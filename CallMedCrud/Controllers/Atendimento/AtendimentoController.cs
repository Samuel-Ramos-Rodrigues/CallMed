using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed class AtendimentoController : Controller
{
    private readonly MKSANContext _context;
    private readonly AtendimentoConversaService _conversas;
    private readonly AtendimentoEnvioService _envio;
    private readonly UsuarioVinculoService _vinculos;

    public AtendimentoController(
        MKSANContext context,
        AtendimentoConversaService conversas,
        AtendimentoEnvioService envio,
        UsuarioVinculoService vinculos)
    {
        _context = context;
        _conversas = conversas;
        _envio = envio;
        _vinculos = vinculos;
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

        return View(new AtendimentoCentralViewModel
        {
            Conversas = resumos,
            ConversaSelecionada = selecionada,
            Mensagens = mensagens,
            PacientesParaVinculo = selecionada is null
                ? Array.Empty<PacienteOpcaoAtendimentoViewModel>()
                : await CarregarPacientesAsync(ct),
            FiltroCanal = canal,
            FiltroModo = modo
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

        if (saida.Status != StatusMensagemAtendimento.Enviada)
        {
            TempData["Erro"] =
                saida.Erro ??
                "A conversa foi criada, mas o provedor não confirmou o envio.";
        }
        else
        {
            TempData["Sucesso"] =
                $"Atendimento iniciado por {NomeCanal(model.Canal)}.";
        }

        return RedirectToAction(
            nameof(Index),
            new { id = conversa.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assumir(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
            return Forbid();

        await _conversas.AssumirAsync(
            conversa,
            usuarioId,
            ct);

        TempData["Sucesso"] = "Atendimento assumido por você.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevolverParaIa(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        await _conversas.DevolverParaIaAsync(
            conversa,
            ct);

        TempData["Sucesso"] = "A conversa voltou para o Assistente CallMed.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Encerrar(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        await _conversas.EncerrarAsync(
            conversa,
            ct);

        TempData["Sucesso"] = "Conversa encerrada.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VincularPaciente(
        long id,
        int pacienteId,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var vinculado = await _conversas.VincularPacienteAsync(
            conversa,
            pacienteId,
            ct);

        TempData[vinculado ? "Sucesso" : "Erro"] = vinculado
            ? "Contato vinculado ao paciente. O contexto seguro da conversa foi renovado."
            : "Não foi possível vincular esse paciente à conversa.";

        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesvincularPaciente(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var desvinculado = await _conversas.DesvincularPacienteAsync(
            conversa,
            ct);

        TempData[desvinculado ? "Sucesso" : "Erro"] = desvinculado
            ? "Contato desvinculado. A próxima mensagem será reidentificada pelo canal."
            : "Conversas do Site/PWA não podem ser desvinculadas manualmente.";

        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(
        long mensagemId,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var falha = await _context.MensagensAtendimento
            .Include(m => m.Conversa)
            .FirstOrDefaultAsync(m =>
                m.Id == mensagemId &&
                m.Direcao == DirecaoMensagemAtendimento.Saida &&
                m.Status == StatusMensagemAtendimento.Falhou,
                ct);

        if (falha?.Conversa is null)
            return NotFound();

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        var nova = await _envio.EnviarAsync(
            falha.Conversa,
            falha.Texto,
            falha.Autor,
            falha.Autor == AutorMensagemAtendimento.Funcionario
                ? userId
                : null,
            falha.Conversa.Assunto,
            ct);

        TempData[nova.Status == StatusMensagemAtendimento.Enviada
            ? "Sucesso"
            : "Erro"] =
            nova.Status == StatusMensagemAtendimento.Enviada
                ? "Mensagem reenviada."
                : nova.Erro ?? "O reenvio falhou novamente.";

        return RedirectToAction(
            nameof(Index),
            new { id = falha.ConversaAtendimentoId });
    }

    [HttpGet]
    public async Task<IActionResult> Atualizacoes(
        long id,
        long afterId = 0,
        CancellationToken ct = default)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var mensagens = await _context.MensagensAtendimento
            .AsNoTracking()
            .Where(m =>
                m.ConversaAtendimentoId == id &&
                m.Id > Math.Max(0, afterId))
            .OrderBy(m => m.Id)
            .Take(80)
            .Select(m => new
            {
                id = m.Id,
                direcao = m.Direcao.ToString(),
                autor = m.Autor.ToString(),
                texto = m.Texto,
                status = m.Status.ToString(),
                erro = m.Erro,
                criadoEm = m.CriadoEm
            })
            .ToListAsync(ct);

        if (mensagens.Count > 0)
        {
            var agora = DateTime.UtcNow;
            await _context.ConversasAtendimento
                .Where(c => c.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.VisualizadaEm, agora),
                    ct);
        }

        return Json(new
        {
            conversaId = conversa.Id,
            modo = conversa.Modo.ToString(),
            ativa = conversa.Ativa,
            responsavelUsuarioId = conversa.ResponsavelUsuarioId,
            mensagens
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarMensagem(
        long id,
        string mensagem,
        CancellationToken ct)
    {
        var texto = (mensagem ?? string.Empty).Trim();

        if (texto.Length == 0)
        {
            TempData["Erro"] = "Digite uma mensagem.";
            return RedirectToAction(nameof(Index), new { id });
        }

        if (texto.Length > 4000)
        {
            TempData["Erro"] = "A mensagem pode ter no máximo 4.000 caracteres.";
            return RedirectToAction(nameof(Index), new { id });
        }

        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (conversa.Modo != ModoAtendimento.Humano ||
            !string.Equals(
                conversa.ResponsavelUsuarioId,
                userId,
                StringComparison.Ordinal))
        {
            await _conversas.AssumirAsync(
                conversa,
                userId,
                ct);
        }

        var saida = await _envio.EnviarAsync(
            conversa,
            texto,
            AutorMensagemAtendimento.Funcionario,
            userId,
            ct: ct);

        if (saida.Status == StatusMensagemAtendimento.Enviada)
            TempData["Sucesso"] = "Mensagem enviada.";
        else
            TempData["Erro"] =
                saida.Erro ??
                "Não foi possível enviar a mensagem.";

        return RedirectToAction(nameof(Index), new { id });
    }
    private async Task<bool> PodeOperarAtendimentoAsync(CancellationToken ct)
    {
        // Admin pode operar a Central mesmo sem um cadastro espelhado em Funcionarios.
        // Para Funcionario, o vínculo precisa existir e estar ativo.
        if (User.IsInRole("Admin"))
            return true;

        var funcionario = await _vinculos.ObterFuncionarioAsync(User, ct);
        return funcionario is { Ativo: true };
    }

    private async Task<IReadOnlyList<PacienteOpcaoAtendimentoViewModel>>
        CarregarPacientesAsync(CancellationToken ct)
    {
        return await _context.Pacientes
            .AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .Select(p => new PacienteOpcaoAtendimentoViewModel
            {
                Id = p.Id,
                Nome = p.Nome,
                Email = p.Email,
                Telefone = p.Telefone,
                PossuiContaWeb = p.UsuarioId != null && p.UsuarioId != ""
            })
            .Take(500)
            .ToListAsync(ct);
    }

    private static string NomeCanal(CanalAtendimento canal) =>
        canal switch
        {
            CanalAtendimento.WhatsApp => "WhatsApp",
            CanalAtendimento.Sms => "SMS",
            CanalAtendimento.Email => "e-mail",
            _ => "site"
        };

}
