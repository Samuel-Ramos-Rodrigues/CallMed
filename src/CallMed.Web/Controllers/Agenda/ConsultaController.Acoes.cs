using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

public partial class ConsultaController : Controller
{

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int id, string? returnUrl = null)
    {
        var item = await _context.Consultas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item is null)
            return NotFound();

        if (!await PodeAcessar(item))
            return Forbid();

        var resultado = await _workflow.ExecuteAsync(id, "confirmar", User, "site", HttpContext.RequestAborted);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> Realizar(int id)
    {
        var resultado = await _workflow.ExecuteAsync(id, "realizar", User, "site", HttpContext.RequestAborted);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> Ausente(int id)
    {
        var resultado = await _workflow.ExecuteAsync(id, "ausente", User, "site", HttpContext.RequestAborted);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, string? returnUrl = null)
    {
        var item = await _context.Consultas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item is null)
            return NotFound();

        if (!await PodeAcessar(item))
            return Forbid();

        var resultado = await _workflow.ExecuteAsync(id, "cancelar", User, "site", HttpContext.RequestAborted);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> MedicosPorEspecialidade(string especialidade)
    {
        if (string.IsNullOrWhiteSpace(especialidade))
            return Json(Array.Empty<object>());

        var medicos = await _especialidades.BuscarMedicosAsync(
            especialidade,
            ct: HttpContext.RequestAborted);

        return Json(medicos.Select(m => new
        {
            id = m.Id,
            nome = m.Nome,
            especialidade = _especialidades.CanonicalizarNome(m.Especialidade),
            texto = $"{m.Nome} — {_especialidades.CanonicalizarNome(m.Especialidade)}"
        }));
    }

    [HttpGet]
    public async Task<IActionResult> OpcoesPorEspecialidade(string especialidade)
    {
        if (string.IsNullOrWhiteSpace(especialidade))
            return Json(Array.Empty<object>());

        var opcoes = await _agendamento.BuscarOpcoesAsync(
            nomeMedico: null,
            especialidade: especialidade,
            dataInicio: null,
            quantidadeDias: 90,
            limite: 3,
            ct: HttpContext.RequestAborted);

        return Json(opcoes.Select(o => new
        {
            medicoId = o.MedicoId,
            medico = o.Medico,
            especialidade = o.Especialidade,
            data = o.Data.ToString("yyyy-MM-dd"),
            horario = o.Horario
        }));
    }

    [HttpGet]
    public async Task<IActionResult> DatasDisponiveis(int medicoId, int? ignorarConsultaId = null)
    {
        var datas = await _agendamento.DatasDisponiveisAsync(
            medicoId,
            ignorarConsultaId,
            HttpContext.RequestAborted);

        return Json(datas.Select(d => new
        {
            value = d.ToString("yyyy-MM-dd"),
            text = d.ToString("dd/MM/yyyy")
        }));
    }

    [HttpGet]
    public async Task<IActionResult> HorariosDisponiveis(
        int medicoId,
        DateTime data,
        int? ignorarConsultaId = null)
    {
        var horarios = await _agendamento.HorariosDisponiveisAsync(
            medicoId,
            data,
            ignorarConsultaId,
            HttpContext.RequestAborted);

        return Json(horarios);
    }

    [HttpGet]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> ConvenioPaciente(int pacienteId)
    {
        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo);

        if (paciente is null)
            return NotFound();

        return Json(new
        {
            paciente.TemConvenio,
            convenioValido = _convenio.EhValido(paciente),
            paciente.NomeConvenio,
            paciente.NumeroConvenio,
            paciente.ValidadeConvenio
        });
    }

    [HttpGet]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> ElegibilidadePaciente(int pacienteId, int especialidadeId)
    {
        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo);
        if (paciente is null) return NotFound();
        var resultado = await _elegibilidadeConvenio.AvaliarAsync(paciente, especialidadeId, HttpContext.RequestAborted);
        return Json(new { resultado.PossuiConvenioValido, resultado.RegrasConfiguradas, resultado.Elegivel, resultado.Mensagem, resultado.StatusConsulta, resultado.RequerAutorizacao, resultado.ObservacaoConsulta });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> AtualizarElegibilidadeConvenio(
        int id, string resultado, string? protocolo, string? observacao, CancellationToken ct)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Paciente)
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (consulta is null) return NotFound();

        var estadoSolicitado = ElegibilidadeConvenioStatus.Todos.FirstOrDefault(x =>
            x.Equals(resultado?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            (x == ElegibilidadeConvenioStatus.Validada ||
             x == ElegibilidadeConvenioStatus.AguardandoAutorizacao ||
             x == ElegibilidadeConvenioStatus.NaoCoberta));
        if (estadoSolicitado is null)
        {
            TempData["Erro"] = "Selecione um resultado válido para a conferência do convênio.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(observacao))
        {
            TempData["Erro"] = "Registre a fonte da conferência ou a justificativa antes de salvar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (estadoSolicitado == ElegibilidadeConvenioStatus.Validada &&
            consulta.TipoPagamento == TipoPagamentoConsulta.Convenio &&
            consulta.Paciente is not null && consulta.Medico is not null)
        {
            var regraAtual = await _elegibilidadeConvenio.AvaliarAsync(
                consulta.Paciente, consulta.Medico.EspecialidadeId, ct);
            if (regraAtual.RequerAutorizacao && string.IsNullOrWhiteSpace(protocolo))
            {
                TempData["Erro"] = "Esta regra exige autorização prévia. Informe o protocolo/número da autorização antes de marcar a elegibilidade como validada.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        if (consulta.TipoPagamento != TipoPagamentoConsulta.Convenio && estadoSolicitado != ElegibilidadeConvenioStatus.NaoCoberta)
        {
            TempData["Erro"] = "Esta consulta não está registrada com pagamento por convênio.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var anterior = new
        {
            consulta.TipoPagamento, consulta.ConvenioUsado, consulta.StatusElegibilidadeConvenio,
            consulta.ProtocoloAutorizacaoConvenio, consulta.ObservacaoElegibilidadeConvenio
        };

        consulta.StatusElegibilidadeConvenio = estadoSolicitado;
        consulta.ObservacaoElegibilidadeConvenio = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()[..Math.Min(observacao.Trim().Length, 600)];
        consulta.ProtocoloAutorizacaoConvenio = string.IsNullOrWhiteSpace(protocolo) ? null : protocolo.Trim()[..Math.Min(protocolo.Trim().Length, 120)];
        consulta.ElegibilidadeConvenioAtualizadaEm = DateTime.UtcNow;
        consulta.ElegibilidadeConvenioAtualizadaPorUsuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (estadoSolicitado == ElegibilidadeConvenioStatus.NaoCoberta)
        {
            // Mantém a vaga e a consulta, mas não deixa um atendimento não coberto faturar no convênio.
            consulta.TipoPagamento = TipoPagamentoConsulta.Particular;
            consulta.ConvenioUsado = null;
            consulta.ProtocoloAutorizacaoConvenio = null;
        }

        await _context.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync(
            "Atualizar elegibilidade do convênio", "Consulta", consulta.Id,
            estadoSolicitado == ElegibilidadeConvenioStatus.NaoCoberta
                ? "Convênio informado como não coberto; consulta mantida e forma de pagamento alterada para particular."
                : $"Situação do convênio atualizada para {ElegibilidadeConvenioStatus.Rotulo(estadoSolicitado)}.",
            anterior, new
            { consulta.TipoPagamento, consulta.ConvenioUsado, consulta.StatusElegibilidadeConvenio,
              consulta.ProtocoloAutorizacaoConvenio, consulta.ObservacaoElegibilidadeConvenio,
              consulta.ElegibilidadeConvenioAtualizadaEm }, ct);

        TempData["Sucesso"] = estadoSolicitado == ElegibilidadeConvenioStatus.NaoCoberta
            ? "A consulta foi mantida e ajustada para atendimento particular. Avise o paciente sobre a alteração."
            : estadoSolicitado == ElegibilidadeConvenioStatus.Validada
                ? "Elegibilidade registrada como validada. A conferência foi auditada."
                : "Consulta atualizada para aguardando autorização. Registre e acompanhe o protocolo com a operadora.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> PodeAcessar(Consulta consulta)
    {
        if (User.IsInRole("Funcionario") || User.IsInRole("Admin"))
            return true;

        var paciente = await ObterPacienteLogado();
        return paciente is not null &&
               paciente.Ativo &&
               consulta.PacienteId == paciente.Id;
    }

    private Task<Paciente?> ObterPacienteLogado() =>
        _vinculos.ObterPacienteAsync(User, HttpContext.RequestAborted);

    private async Task CarregarFormularioAsync(
        Consulta consulta,
        string? especialidadeSelecionada = null,
        string? tipoPagamentoSelecionado = null)
    {
        if (User.IsInRole("Funcionario") || User.IsInRole("Admin"))
        {
            ViewBag.PacienteId = new SelectList(
                await _context.Pacientes
                    .AsNoTracking()
                    .Where(p => p.Ativo)
                    .OrderBy(p => p.Nome)
                    .ToListAsync(),
                "Id",
                "Nome",
                consulta.PacienteId);
        }
        if (User.IsInRole("Paciente"))
        {
            var pacienteLogado = await ObterPacienteLogado();
            ViewBag.PacienteTemConvenio = pacienteLogado?.TemConvenio == true;
            ViewBag.PacienteConvenioValido = pacienteLogado is not null && _convenio.EhValido(pacienteLogado);
            ViewBag.PacienteNomeConvenio = pacienteLogado?.NomeConvenio;
            ViewBag.PacienteValidadeConvenio = pacienteLogado?.ValidadeConvenio;
        }

        if (string.IsNullOrWhiteSpace(especialidadeSelecionada) && consulta.MedicoId > 0)
        {
            var especialidadeMedico = await _context.Medicos
                .AsNoTracking()
                .Where(m => m.Id == consulta.MedicoId)
                .Select(m => m.Especialidade)
                .FirstOrDefaultAsync();

            especialidadeSelecionada = _especialidades.CanonicalizarNome(especialidadeMedico);
        }

        var especialidades = await _especialidades.ListarAtivasAsync(HttpContext.RequestAborted);
        ViewBag.Especialidades = especialidades;
        ViewBag.EspecialidadeSelecionada = especialidadeSelecionada ?? string.Empty;

        var medicos = string.IsNullOrWhiteSpace(especialidadeSelecionada)
            ? new List<Medico>()
            : await _especialidades.BuscarMedicosAsync(
                especialidadeSelecionada,
                ct: HttpContext.RequestAborted);

        ViewBag.MedicoId = new SelectList(
            medicos.Select(m => new
            {
                m.Id,
                Texto = $"{m.Nome} — {_especialidades.CanonicalizarNome(m.Especialidade)}"
            }),
            "Id",
            "Texto",
            consulta.MedicoId);

        ViewBag.TipoPagamentoSelecionado = tipoPagamentoSelecionado ?? consulta.TipoPagamento;
    }
}
