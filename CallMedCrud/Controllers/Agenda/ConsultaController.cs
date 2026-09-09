using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Paciente,Funcionario,Admin")]
public class ConsultaController : Controller
{
    private readonly MKSANContext _context;
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
        IClinicaClock clock)
    {
        _context = context;
        _agendamento = agendamento;
        _especialidades = especialidades;
        _vinculos = vinculos;
        _convenio = convenio;
        _elegibilidadeConvenio = elegibilidadeConvenio;
        _solicitacoes = solicitacoes;
        _auditoria = auditoria;
        _clock = clock;
    }

    public async Task<IActionResult> Index(string? status)
    {
        var query = _context.Consultas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Medico)
            .AsQueryable();

        if (User.IsInRole("Paciente"))
        {
            var paciente = await ObterPacienteLogado();
            if (paciente is null || !paciente.Ativo)
                return Forbid();

            query = query.Where(c => c.PacienteId == paciente.Id);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            ConsultaStatus.Todos.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            var statusNormalizado = ConsultaStatus.Todos
                .First(x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
            query = query.Where(c => c.Status == statusNormalizado);
            ViewBag.Status = statusNormalizado;
        }

        ViewBag.HojeClinica = _clock.Hoje;
        return View(await query
            .OrderByDescending(c => c.Data)
            .ThenBy(c => c.Horario)
            .ToListAsync());
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
            tipoPagamento = null;
        }
        else
        {
            paciente = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.Id == consulta.PacienteId && p.Ativo);
        }

        if (paciente is null)
            ModelState.AddModelError(nameof(Consulta.PacienteId), "Selecione um paciente ativo.");

        if (consulta.MedicoId <= 0)
            ModelState.AddModelError(nameof(Consulta.MedicoId), "Selecione um médico.");

        if (!ModelState.IsValid)
        {
            ViewBag.SolicitacaoId = solicitacaoId;
            await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
            return View(consulta);
        }

        if (ehPaciente && paciente is not null && paciente.TemConvenio && !_convenio.EhValido(paciente))
        {
            var especialidadePendente = await _context.Medicos.AsNoTracking()
                .Where(x => x.Id == consulta.MedicoId)
                .Select(x => x.EspecialidadeId)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);

            var pedido = await _solicitacoes.CriarAsync(
                MKSANCrud.Models.Atendimento.CanalAtendimento.Web,
                paciente.Id, especialidadePendente, consulta.MedicoId, consulta.Data, "Qualquer",
                $"Paciente tentou agendar {consulta.Data:dd/MM/yyyy} às {consulta.Horario} pelo autoatendimento, mas o convênio está vencido ou incompleto.",
                ct: HttpContext.RequestAborted);
            await _solicitacoes.TriarAsync(
                pedido.Id, paciente.Id, especialidadePendente, consulta.MedicoId, null, null,
                atendimentoParticular: false, liberarSemMatriz: false, responsavelUsuarioId: null,
                ct: HttpContext.RequestAborted);
            TempData["Sucesso"] = "Sua solicitação foi recebida. Seu convênio precisa ser revisado pela equipe antes da confirmação. Se preferir atendimento particular, informe isso ao atendimento.";
            return RedirectToAction("Index", "Home");
        }

        if (ehPaciente && paciente is not null && _convenio.EhValido(paciente))
        {
            var especialidadeIdMedico = await _context.Medicos.AsNoTracking()
                .Where(x => x.Id == consulta.MedicoId)
                .Select(x => x.EspecialidadeId)
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (especialidadeIdMedico.HasValue)
            {
                var elegibilidadeDireta = await _elegibilidadeConvenio.AvaliarAsync(paciente, especialidadeIdMedico, HttpContext.RequestAborted);
                if (!elegibilidadeDireta.RegrasConfiguradas || !elegibilidadeDireta.Elegivel)
                {
                    var pedido = await _solicitacoes.CriarAsync(
                        MKSANCrud.Models.Atendimento.CanalAtendimento.Web,
                        paciente.Id, especialidadeIdMedico, consulta.MedicoId, consulta.Data, "Qualquer",
                        $"Paciente tentou agendar {consulta.Data:dd/MM/yyyy} às {consulta.Horario} pelo autoatendimento; é necessária triagem do convênio.",
                        ct: HttpContext.RequestAborted);
                    await _solicitacoes.TriarAsync(
                        pedido.Id, paciente.Id, especialidadeIdMedico, consulta.MedicoId, null, null,
                        atendimentoParticular: false, liberarSemMatriz: false, responsavelUsuarioId: null,
                        ct: HttpContext.RequestAborted);
                    TempData["Sucesso"] = "Sua solicitação foi recebida. A equipe CallMed vai validar a cobertura do seu convênio antes de confirmar o horário. A vaga ainda não está reservada.";
                    return RedirectToAction("Index", "Home");
                }
            }
        }

        var liberarConvenioSemMatriz = false;
        if (solicitacaoId.HasValue && !ehPaciente)
        {
            var solicitacaoOrigem = await _context.SolicitacoesAtendimento.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == solicitacaoId.Value, HttpContext.RequestAborted);

            if (solicitacaoOrigem is null ||
                solicitacaoOrigem.Status != StatusSolicitacaoAtendimento.BuscandoHorario ||
                !solicitacaoOrigem.PacienteId.HasValue ||
                !solicitacaoOrigem.EspecialidadeId.HasValue ||
                solicitacaoOrigem.PacienteId.Value != consulta.PacienteId ||
                solicitacaoOrigem.ElegivelConvenio == false ||
                !string.IsNullOrWhiteSpace(solicitacaoOrigem.PendenciaTriagem))
            {
                ModelState.AddModelError(string.Empty, "A solicitação não está liberada pela triagem para concluir este agendamento.");
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
                ModelState.AddModelError(string.Empty, "O médico escolhido não corresponde ao que foi aprovado na triagem.");
                ViewBag.SolicitacaoId = solicitacaoId;
                await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
                return View(consulta);
            }

            liberarConvenioSemMatriz = solicitacaoOrigem.ElegivelConvenio == true &&
                !string.Equals(solicitacaoOrigem.ConvenioInformado, "Particular", StringComparison.OrdinalIgnoreCase);

            if (liberarConvenioSemMatriz)
            {
                var elegibilidadeAtual = await _elegibilidadeConvenio.AvaliarAsync(paciente!, solicitacaoOrigem.EspecialidadeId, HttpContext.RequestAborted);
                if (!elegibilidadeAtual.RegrasConfiguradas && string.IsNullOrWhiteSpace(solicitacaoOrigem.JustificativaLiberacao))
                {
                    ModelState.AddModelError(string.Empty, "A liberação sem matriz precisa de uma justificativa registrada na triagem.");
                    ViewBag.SolicitacaoId = solicitacaoId;
                    await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
                    return View(consulta);
                }
                if (elegibilidadeAtual.RegrasConfiguradas && !elegibilidadeAtual.Elegivel)
                {
                    ModelState.AddModelError(string.Empty, elegibilidadeAtual.Mensagem);
                    ViewBag.SolicitacaoId = solicitacaoId;
                    await CarregarFormularioAsync(consulta, especialidade, tipoPagamento);
                    return View(consulta);
                }
            }
        }

        var resultado = await _agendamento.AgendarAsync(
            consulta.PacienteId,
            consulta.MedicoId,
            consulta.Data,
            consulta.Horario,
            consulta.Observacao,
            tipoPagamento,
            permitirEscolhaPagamento: !ehPaciente,
            ct: HttpContext.RequestAborted,
            permitirConvenioSemMatriz: liberarConvenioSemMatriz);

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
        if (solicitacaoId.HasValue && !ehPaciente)
            return RedirectToAction("Triagem", "Solicitacoes", new { id = solicitacaoId.Value });
        return RedirectToAction(nameof(Index));
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
        TempData["Sucesso"] = resultado.Mensagem + " O médico foi mantido.";
        return RedirectToAction(nameof(Index));
    }

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

        var resultado = await _agendamento.ConfirmarAsync(id, HttpContext.RequestAborted);
        if (resultado.Sucesso) await _auditoria.RegistrarAsync("Confirmar", "Consulta", id, "Consulta confirmada.", ct: HttpContext.RequestAborted);
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
        var resultado = await _agendamento.RealizarAsync(id, HttpContext.RequestAborted);
        if (resultado.Sucesso) { await _auditoria.RegistrarAsync("Realizar", "Consulta", id, "Consulta marcada como realizada.", ct: HttpContext.RequestAborted); await _solicitacoes.AtualizarPorConsultaAsync(id, StatusSolicitacaoAtendimento.Encerrada, HttpContext.RequestAborted); }
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> Ausente(int id)
    {
        var resultado = await _agendamento.MarcarAusenteAsync(id, HttpContext.RequestAborted);
        if (resultado.Sucesso) { await _auditoria.RegistrarAsync("Ausência", "Consulta", id, "Paciente marcado como ausente.", ct: HttpContext.RequestAborted); await _solicitacoes.AtualizarPorConsultaAsync(id, StatusSolicitacaoAtendimento.Encerrada, HttpContext.RequestAborted); }
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

        var resultado = await _agendamento.CancelarAsync(id, HttpContext.RequestAborted);
        if (resultado.Sucesso) { await _auditoria.RegistrarAsync("Cancelar", "Consulta", id, "Consulta cancelada e slot liberado.", ct: HttpContext.RequestAborted); await _solicitacoes.AtualizarPorConsultaAsync(id, StatusSolicitacaoAtendimento.Cancelada, HttpContext.RequestAborted); }
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
        return Json(new { resultado.PossuiConvenioValido, resultado.RegrasConfiguradas, resultado.Elegivel, resultado.Mensagem });
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
