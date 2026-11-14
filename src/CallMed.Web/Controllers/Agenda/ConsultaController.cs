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

[Authorize(Roles = "Paciente,Funcionario,Admin")]
public partial class ConsultaController : Controller
{
    private readonly MKSANContext _context;
    private readonly MKSANCrud.Application.Queries.ConsultaListQuery _listQuery;
    private readonly MKSANCrud.Application.Consultas.ConsultaWorkflow _workflow;
    private readonly AgendamentoService _agendamento;
    private readonly EspecialidadeService _especialidades;
    private readonly UsuarioVinculoService _vinculos;
    private readonly ConvenioService _convenio;
    private readonly ConvenioElegibilidadeService _elegibilidadeConvenio;
    private readonly SolicitacaoAtendimentoService _solicitacoes;
    private readonly AuditoriaService _auditoria;
    private readonly IClinicaClock _clock;

    public ConsultaController(
        MKSANContext context,
        AgendamentoService agendamento,
        EspecialidadeService especialidades,
        UsuarioVinculoService vinculos,
        ConvenioService convenio,
        ConvenioElegibilidadeService elegibilidadeConvenio,
        SolicitacaoAtendimentoService solicitacoes,
        AuditoriaService auditoria,
        IClinicaClock clock,
        MKSANCrud.Application.Queries.ConsultaListQuery listQuery,
        MKSANCrud.Application.Consultas.ConsultaWorkflow workflow)
    {
        _context = context;
        _listQuery = listQuery;
        _workflow = workflow;
        _agendamento = agendamento;
        _especialidades = especialidades;
        _vinculos = vinculos;
        _convenio = convenio;
        _elegibilidadeConvenio = elegibilidadeConvenio;
        _solicitacoes = solicitacoes;
        _auditoria = auditoria;
        _clock = clock;
    }

    public async Task<IActionResult> Index(string? status, string? statusConvenio,
        string? busca = null, string? canal = null, string? situacao = null,
        int? pagina = null, CancellationToken ct = default)
    {
        var result = await _listQuery.ExecuteAsync(User, status, statusConvenio, busca, canal, situacao, pagina, ct);
        if (result.AcessoNegado) return Forbid();
        foreach (var item in result.Metadados) ViewData[item.Key] = item.Value;
        return result.Equipe is not null ? View("Equipe", result.Equipe) : View(result.Paciente);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item is null)
            return NotFound();

        if (!await PodeAcessar(item))
            return Forbid();

        ViewBag.HojeClinica = _clock.Hoje;
        return View(item);
    }

    public async Task<IActionResult> Create(
        int? medicoId = null,
        DateTime? data = null,
        string? horario = null,
        int? pacienteId = null,
        string? especialidade = null,
        string? tipoPagamento = null,
        int? solicitacaoId = null)
    {
        var consulta = new Consulta
        {
            Data = data?.Date ?? _clock.Hoje,
            Horario = horario?.Trim() ?? string.Empty,
            MedicoId = medicoId.GetValueOrDefault(),
            PacienteId = pacienteId.GetValueOrDefault(),
            Status = ConsultaStatus.Pendente
        };

        if (User.IsInRole("Paciente"))
        {
            var paciente = await ObterPacienteLogado();
            if (paciente is null || !paciente.Ativo)
                return Forbid();

            consulta.PacienteId = paciente.Id;
            _convenio.AplicarPagamento(consulta, paciente);
            solicitacaoId = null;
        }

        ViewBag.SolicitacaoId = solicitacaoId;
        await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
        return View(consulta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("PacienteId,MedicoId,Data,Horario,Observacao")] Consulta consulta,
        string? especialidade,
        string? tipoPagamento,
        int? solicitacaoId)
    {
        Paciente? paciente;
        var ehPaciente = User.IsInRole("Paciente");

        if (ehPaciente)
        {
            paciente = await ObterPacienteLogado();
            if (paciente is null || !paciente.Ativo)
                return Forbid();

            consulta.PacienteId = paciente.Id;
            // Mantém a forma de pagamento escolhida no formulário. O serviço
            // valida novamente a validade do convênio antes de gravar.
        }
        else
        {
            paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.Id == consulta.PacienteId && p.Ativo);
        }

        if (paciente is null)
            ModelState.AddModelError(nameof(Consulta.PacienteId), "Selecione um paciente ativo.");

        if (consulta.MedicoId <= 0)
            ModelState.AddModelError(nameof(Consulta.MedicoId), "Selecione um psicólogo.");

        if (!ModelState.IsValid)
        {
            ViewBag.SolicitacaoId = solicitacaoId;
            await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
            return View(consulta);
        }

        if (solicitacaoId.HasValue && !ehPaciente)
        {
            var solicitacaoOrigem = await _context.SolicitacoesAtendimento.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == solicitacaoId.Value, HttpContext.RequestAborted);

            if (solicitacaoOrigem is null ||
                solicitacaoOrigem.Status != StatusSolicitacaoAtendimento.BuscandoHorario ||
                !solicitacaoOrigem.PacienteId.HasValue ||
                !solicitacaoOrigem.EspecialidadeId.HasValue ||
                solicitacaoOrigem.PacienteId.Value != consulta.PacienteId)
            {
                ModelState.AddModelError(string.Empty, "Confira os dados da solicitação antes de escolher uma vaga.");
                ViewBag.SolicitacaoId = solicitacaoId;
                await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
                return View(consulta);
            }

            var especialidadeDoMedico = await _context.Medicos.AsNoTracking()
                .Where(x => x.Id == consulta.MedicoId && x.Ativo)
                .Select(x => x.EspecialidadeId)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            if (!especialidadeDoMedico.HasValue || especialidadeDoMedico.Value != solicitacaoOrigem.EspecialidadeId.Value ||
                (solicitacaoOrigem.MedicoId.HasValue && solicitacaoOrigem.MedicoId.Value != consulta.MedicoId))
            {
                ModelState.AddModelError(string.Empty, "O psicólogo escolhido não corresponde à especialidade ou à preferência registrada na solicitação.");
                ViewBag.SolicitacaoId = solicitacaoId;
                await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
                return View(consulta);
            }

        }

        var resultado = await _agendamento.AgendarAsync(
            consulta.PacienteId,
            consulta.MedicoId,
            consulta.Data,
            consulta.Horario,
            consulta.Observacao,
            tipoPagamento,
            permitirEscolhaPagamento: true,
            ct: HttpContext.RequestAborted);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensagem);
            ViewBag.SolicitacaoId = solicitacaoId;
            await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
            return View(consulta);
        }

        if (resultado.Consulta is not null)
        {
            if (solicitacaoId.HasValue && !ehPaciente)
                await _solicitacoes.VincularConsultaAsync(solicitacaoId.Value, resultado.Consulta.Id, HttpContext.RequestAborted);
            else if (ehPaciente)
                await _solicitacoes.RegistrarAgendamentoDiretoAsync(MKSANCrud.Models.Atendimento.CanalAtendimento.Web, resultado.Consulta.PacienteId, resultado.Consulta.MedicoId, resultado.Consulta.Id, "Agendamento realizado pelo PWA/site.", HttpContext.RequestAborted);
            await _auditoria.RegistrarAsync("Agendar", "Consulta", resultado.Consulta.Id, $"Consulta criada para o paciente #{resultado.Consulta.PacienteId}.", novo: new { resultado.Consulta.MedicoId, resultado.Consulta.Data, resultado.Consulta.Horario, resultado.Consulta.Status }, ct: HttpContext.RequestAborted);
        }

        TempData["Sucesso"] = resultado.Mensagem;
        return resultado.Consulta is not null
            ? RedirectToAction(nameof(Details), new { id = resultado.Consulta.Id })
            : RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Consultas
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item is null)
            return NotFound();

        if (!ConsultaStatus.PodeRemarcar(item.Status))
        {
            TempData["Erro"] = "Consultas canceladas ou realizadas não podem ser editadas.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await CarregarFormularioAsync(
            item,
            item.Medico is null ? null : _especialidades.CanonicalizarNome(item.Medico.Especialidade),
            item.TipoPagamento);

        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,PacienteId,MedicoId,Data,Horario,Observacao")] Consulta model,
        string? especialidade,
        string? tipoPagamento)
    {
        if (id != model.Id)
            return NotFound();

        if (!ModelState.IsValid)
        {
            await CarregarFormularioAsync(model, especialidade, tipoPagamento);
            return View(model);
        }

        var resultado = await _agendamento.EditarAsync(
            id,
            model.PacienteId,
            model.MedicoId,
            model.Data,
            model.Horario,
            model.Observacao,
            tipoPagamento,
            HttpContext.RequestAborted);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensagem);
            await CarregarFormularioAsync(model, especialidade, tipoPagamento);
            return View(model);
        }

        if (resultado.Consulta is not null)
            await _auditoria.RegistrarAsync("Editar", "Consulta", id, "Consulta atualizada pela equipe.", novo: new { resultado.Consulta.PacienteId, resultado.Consulta.MedicoId, resultado.Consulta.Data, resultado.Consulta.Horario }, ct: HttpContext.RequestAborted);
        TempData["Sucesso"] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Remarcar(int id)
    {
        var item = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item is null)
            return NotFound();

        if (!await PodeAcessar(item))
            return Forbid();

        if (!ConsultaStatus.PodeRemarcar(item.Status))
        {
            TempData["Erro"] = "Essa consulta não pode mais ser remarcada.";
            return RedirectToAction(nameof(Index));
        }

        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remarcar(int id, DateTime data, string horario)
    {
        var item = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item is null)
            return NotFound();

        if (!await PodeAcessar(item))
            return Forbid();

        var resultado = await _agendamento.RemarcarAsync(
            id,
            data,
            horario,
            HttpContext.RequestAborted);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensagem);
            item.Data = data;
            item.Horario = horario;
            return View(item);
        }

        if (resultado.Consulta is not null)
            await _auditoria.RegistrarAsync("Remarcar", "Consulta", id, $"Consulta remarcada para {resultado.Consulta.Data:dd/MM/yyyy} às {resultado.Consulta.Horario}.", ct: HttpContext.RequestAborted);
        TempData["Sucesso"] = resultado.Mensagem + " O psicólogo foi mantido.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
