using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed partial class AtendimentoController : Controller
{
    private readonly MKSANContext _context;
    private readonly AtendimentoConversaService _conversas;
    private readonly AtendimentoEnvioService _envio;
    private readonly AtendimentoOrquestradorService _orquestrador;
    private readonly UsuarioVinculoService _vinculos;
    private readonly AtendimentoFeedbackService? _feedback;
    private readonly IClinicaClock _clock;

    public AtendimentoController(
        MKSANContext context,
        AtendimentoConversaService conversas,
        AtendimentoEnvioService envio,
        AtendimentoOrquestradorService orquestrador,
        UsuarioVinculoService vinculos, AtendimentoFeedbackService? feedback = null, IClinicaClock? clock = null)
    {
        _context = context;
        _conversas = conversas;
        _envio = envio;
        _orquestrador = orquestrador;
        _vinculos = vinculos;
        _feedback = feedback;
        _clock = clock ?? new ClinicaClock();
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        long? id,
        string? canal,
        string? modo,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var query = _context.ConversasAtendimento
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Where(c =>
                c.PacienteId != null ||
                c.Canal != CanalAtendimento.Web)
            .AsQueryable();

        if (Enum.TryParse<CanalAtendimento>(
                canal,
                true,
                out var canalEnum))
        {
            query = query.Where(c => c.Canal == canalEnum);
        }

        if (Enum.TryParse<ModoAtendimento>(
                modo,
                true,
                out var modoEnum))
        {
            query = query.Where(c => c.Modo == modoEnum);
        }

        var listaBase = await query
            .OrderByDescending(c => c.Ativa)
            .ThenByDescending(c => c.UltimaInteracaoEm)
            .Take(120)
            .ToListAsync(ct);

        var ids = listaBase.Select(c => c.Id).ToArray();

        var mensagensResumo = await _context.MensagensAtendimento
            .AsNoTracking()
            .Where(m => ids.Contains(m.ConversaAtendimentoId))
            .Select(m => new
            {
                m.Id,
                m.ConversaAtendimentoId,
                m.Direcao,
                m.Texto,
                m.CriadoEm
            })
            .ToListAsync(ct);

        var gruposMensagens = mensagensResumo
            .GroupBy(m => m.ConversaAtendimentoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ultimasMensagens = new Dictionary<long, string>();
        var naoLidas = new Dictionary<long, int>();

        foreach (var conversa in listaBase)
        {
            if (!gruposMensagens.TryGetValue(
                    conversa.Id,
                    out var grupo))
            {
                ultimasMensagens[conversa.Id] = string.Empty;
                naoLidas[conversa.Id] = 0;
                continue;
            }

            ultimasMensagens[conversa.Id] = grupo
                .OrderByDescending(m => m.CriadoEm)
                .ThenByDescending(m => m.Id)
                .Select(m => m.Texto)
                .FirstOrDefault() ??
                string.Empty;

            naoLidas[conversa.Id] = grupo.Count(m =>
                m.Direcao == DirecaoMensagemAtendimento.Entrada &&
                (!conversa.VisualizadaEm.HasValue ||
                 m.CriadoEm > conversa.VisualizadaEm.Value));
        }

        var resumos = listaBase
            .Select(c => new ConversaResumoViewModel
            {
                Id = c.Id,
                Nome = c.Paciente?.Nome ??
                       c.IdentificadorExterno,
                Identificador = c.IdentificadorExterno,
                Canal = c.Canal,
                Modo = c.Modo,
                Ativa = c.Ativa,
                UltimaInteracaoEm = c.UltimaInteracaoEm,
                UltimaMensagem = ultimasMensagens.TryGetValue(
                    c.Id,
                    out var texto)
                    ? texto
                    : string.Empty,
                NaoLidas = naoLidas.TryGetValue(c.Id, out var qtd)
                    ? qtd
                    : 0
            })
            .ToList();

        ConversaAtendimento? selecionada = null;
        IReadOnlyList<MensagemAtendimento> mensagens =
            Array.Empty<MensagemAtendimento>();

        var idSelecionado = id ??
                            resumos.FirstOrDefault()?.Id;

        if (idSelecionado.HasValue)
        {
            selecionada = await _context.ConversasAtendimento
                .Include(c => c.Paciente)
                .Include(c => c.ResponsavelUsuario)
                .FirstOrDefaultAsync(
                    c => c.Id == idSelecionado.Value,
                    ct);

            if (selecionada is not null)
            {
                mensagens = await _conversas.CarregarMensagensAsync(
                    selecionada.Id,
                    160,
                    ct);

                selecionada.VisualizadaEm = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
        }

        var consultasPaciente = selecionada?.PacienteId is int pacienteId
            ? await _context.Consultas.AsNoTracking()
                .Where(c => c.PacienteId == pacienteId)
                .OrderByDescending(c => c.Data).ThenByDescending(c => c.Horario)
                .Select(c => new ConsultaContextoAtendimento(c.Id, c.Data, c.Horario,
                    c.Medico == null ? "Profissional não definido" : c.Medico.Nome,
                    c.Medico == null ? "Consulta" : c.Medico.Especialidade ?? "Consulta", c.Status))
                .Take(12).ToListAsync(ct)
            : new List<ConsultaContextoAtendimento>();

        var hojeClinica = _clock.Hoje;
        var horarioAtual = _clock.Agora.ToString("HH:mm");
        var proximaConsulta = selecionada?.PacienteId is int proximoPacienteId
            ? await _context.Consultas.AsNoTracking()
                .Where(c => c.PacienteId == proximoPacienteId && (c.Data > hojeClinica ||
                    (c.Data == hojeClinica && string.Compare(c.Horario, horarioAtual) >= 0)) &&
                    c.Status != "Cancelada" && c.Status != "Realizada" && c.Status != "Ausente")
                .OrderBy(c => c.Data).ThenBy(c => c.Horario)
                .Select(c => new ConsultaContextoAtendimento(c.Id, c.Data, c.Horario,
                    c.Medico == null ? "Profissional não definido" : c.Medico.Nome,
                    c.Medico == null ? "Consulta" : c.Medico.Especialidade ?? "Consulta", c.Status))
                .FirstOrDefaultAsync(ct)
            : null;

        return View(new AtendimentoCentralViewModel
        {
            Conversas = resumos,
            ConversaSelecionada = selecionada,
            EstadoIa = selecionada is null ? null : await _conversas.ObterEstadoIaAsync(selecionada, ct),
            Mensagens = mensagens,
            PacientesParaVinculo = selecionada is null
                ? Array.Empty<PacienteOpcaoAtendimentoViewModel>()
                : await CarregarPacientesAsync(ct),
            FiltroCanal = canal,
            FiltroModo = modo,
            ConsultasPaciente = consultasPaciente,
            ProximaConsulta = proximaConsulta
        });
    }

    [HttpGet]
    public async Task<IActionResult> Nova(
        CancellationToken ct = default)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var model = new NovaConversaAtendimentoViewModel
        {
            Pacientes = await CarregarPacientesAsync(ct)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nova(
        NovaConversaAtendimentoViewModel model,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        if (model.Canal is not (
                CanalAtendimento.Web or
                CanalAtendimento.WhatsApp or
                CanalAtendimento.Sms or
                CanalAtendimento.Email))
        {
            ModelState.AddModelError(
                nameof(model.Canal),
                "Canal de atendimento inválido.");
        }

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == model.PacienteId && p.Ativo,
                ct);

        if (paciente is null)
        {
            ModelState.AddModelError(
                nameof(model.PacienteId),
                "Paciente não encontrado ou inativo.");
        }

        string? destinatario = null;

        if (paciente is not null)
        {
            destinatario = model.Canal switch
            {
                CanalAtendimento.Web => paciente.UsuarioId,
                CanalAtendimento.WhatsApp => paciente.Telefone,
                CanalAtendimento.Sms => paciente.Telefone,
                CanalAtendimento.Email => paciente.Email,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(destinatario))
            {
                var campo = model.Canal switch
                {
                    CanalAtendimento.Web => "conta vinculada ao site",
                    CanalAtendimento.Email => "e-mail",
                    _ => "telefone"
                };

                ModelState.AddModelError(
                    nameof(model.Canal),
                    $"Esse paciente não possui {campo} disponível para o canal selecionado.");
            }
        }

        if (!_envio.CanalConfigurado(model.Canal))
        {
            ModelState.AddModelError(
                nameof(model.Canal),
                "Esse canal ainda não está configurado para envio.");
        }

        if (!ModelState.IsValid ||
            paciente is null ||
            string.IsNullOrWhiteSpace(destinatario))
        {
            model.Pacientes = await CarregarPacientesAsync(ct);
            return View(model);
        }

        var conversa = await _conversas.ObterOuCriarAsync(
            model.Canal,
            destinatario,
            paciente.Id,
            model.Assunto,
            ct: ct);

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        await _conversas.AssumirAsync(
            conversa,
            userId,
            ct);

        var saida = await _envio.EnviarAsync(
            conversa,
            model.Mensagem.Trim(),
            AutorMensagemAtendimento.Funcionario,
            userId,
            model.Assunto,
            ct);

        if (saida.Status is not (StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue or StatusMensagemAtendimento.AguardandoEnvio))
        {
            TempData["Erro"] =
                saida.Erro ??
                "A conversa foi criada, mas o provedor não confirmou o envio.";
        }
        else
        {
            TempData["Sucesso"] =
                saida.Status == StatusMensagemAtendimento.AguardandoEnvio
                    ? "Atendimento iniciado. Mensagem na fila de SMS; aguarde o envio pelo aparelho."
                    : $"Atendimento iniciado por {NomeCanal(model.Canal)}.";
        }

        return RedirectToAction(
            nameof(Index),
            new { id = conversa.Id });
    }
}
