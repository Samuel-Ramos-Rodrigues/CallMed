using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed class SolicitacoesController : Controller
{
    private readonly MKSANContext _context;
    private readonly SolicitacaoAtendimentoService _service;
    private readonly ConvenioElegibilidadeService _elegibilidade;

    public SolicitacoesController(
        MKSANContext context,
        SolicitacaoAtendimentoService service,
        ConvenioElegibilidadeService elegibilidade)
    {
        _context = context;
        _service = service;
        _elegibilidade = elegibilidade;
    }

    public async Task<IActionResult> Index(string? busca, string? canal, CancellationToken ct)
    {
        var query = _context.SolicitacoesAtendimento
            .AsNoTracking()
            .Include(x => x.Paciente)
            .Include(x => x.Especialidade)
            .Include(x => x.Medico)
            .Include(x => x.Consulta)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            query = query.Where(x =>
                (x.Paciente != null && (x.Paciente.Nome.ToLower().Contains(termo) || x.Paciente.Cpf.Contains(termo))) ||
                (x.NomeContato != null && x.NomeContato.ToLower().Contains(termo)) ||
                (x.TelefoneContato != null && x.TelefoneContato.Contains(termo)) ||
                (x.EmailContato != null && x.EmailContato.ToLower().Contains(termo)));
        }

        if (!string.IsNullOrWhiteSpace(canal) && Enum.TryParse<CanalAtendimento>(canal, true, out var canalEnum))
            query = query.Where(x => x.Canal == canalEnum);

        var itens = await query
            .OrderBy(x => x.Status == StatusSolicitacaoAtendimento.Confirmada || x.Status == StatusSolicitacaoAtendimento.Encerrada)
            .ThenBy(x => x.CriadoEm)
            .Take(400)
            .ToListAsync(ct);

        ViewData["Title"] = "Fluxo de solicitações";
        ViewData["Subtitle"] = "Todos os canais no mesmo processo: solicitação, triagem, horário e confirmação.";
        return View(new SolicitacoesPainelViewModel { Itens = itens, Busca = busca, Canal = canal });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PrepararSeletoresAsync(ct);
        ViewData["Title"] = "Nova solicitação";
        ViewData["Subtitle"] = "Registre também atendimentos por telefone ou presenciais.";
        return View(new NovaSolicitacaoViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NovaSolicitacaoViewModel model, CancellationToken ct)
    {
        if (!model.PacienteId.HasValue &&
            string.IsNullOrWhiteSpace(model.NomeContato) &&
            string.IsNullOrWhiteSpace(model.TelefoneContato) &&
            string.IsNullOrWhiteSpace(model.EmailContato))
        {
            ModelState.AddModelError(string.Empty, "Selecione um paciente ou informe pelo menos um dado do contato.");
        }

        if (!ModelState.IsValid)
        {
            await PrepararSeletoresAsync(ct);
            return View(model);
        }

        try
        {
            var item = await _service.CriarAsync(
                model.Canal,
                model.PacienteId,
                model.EspecialidadeId,
                model.MedicoId,
                model.DataPreferida,
                model.PeriodoPreferido,
                model.Observacao,
                model.NomeContato,
                model.TelefoneContato,
                model.EmailContato,
                ct: ct);

            TempData["Sucesso"] = "Solicitação registrada e adicionada ao fluxo operacional.";
            return RedirectToAction(nameof(Triagem), new { id = item.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PrepararSeletoresAsync(ct);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Triagem(int id, CancellationToken ct)
    {
        var item = await CarregarAsync(id, ct);
        if (item is null) return NotFound();

        await PrepararSeletoresAsync(ct, item.PacienteId, item.EspecialidadeId, item.MedicoId);
        ResultadoElegibilidadeConvenio? elegibilidade = null;
        if (item.Paciente is not null)
            elegibilidade = await _elegibilidade.AvaliarAsync(item.Paciente, item.EspecialidadeId, ct);

        var historico = await _context.AuditoriaEventos
            .AsNoTracking()
            .Where(x => x.Entidade == "Solicitação" && x.EntidadeId == id.ToString())
            .OrderByDescending(x => x.CriadoEm)
            .Take(30)
            .ToListAsync(ct);

        ViewData["Title"] = $"Triagem #{id}";
        ViewData["Subtitle"] = "Confira cadastro, convênio, especialidade e pendências antes de buscar uma vaga.";
        return View(new TriagemSolicitacaoViewModel { Solicitacao = item, Elegibilidade = elegibilidade, Historico = historico });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Triagem(
        int id,
        int? pacienteId,
        int? especialidadeId,
        int? medicoId,
        string? pendenciaTriagem,
        string? justificativaLiberacao,
        bool atendimentoParticular,
        bool liberarSemMatriz,
        CancellationToken ct)
    {
        var estado = await _context.SolicitacoesAtendimento.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Status, x.ConsultaId })
            .FirstOrDefaultAsync(ct);
        if (estado is null) return NotFound();
        if (estado.ConsultaId.HasValue || estado.Status is StatusSolicitacaoAtendimento.Confirmada or StatusSolicitacaoAtendimento.Cancelada or StatusSolicitacaoAtendimento.Encerrada)
        {
            TempData["Erro"] = "Esta solicitação já foi finalizada ou possui consulta vinculada. O histórico fica bloqueado para evitar inconsistências.";
            return RedirectToAction(nameof(Triagem), new { id });
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var resultado = await _service.TriarAsync(id, pacienteId, especialidadeId, medicoId, pendenciaTriagem, justificativaLiberacao, atendimentoParticular, liberarSemMatriz, usuarioId, ct);
        TempData[resultado.Sucesso ? "Sucesso" : "Erro"] = resultado.Mensagem;
        return RedirectToAction(nameof(Triagem), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuscarHorario(int id, CancellationToken ct)
    {
        var item = await CarregarAsync(id, ct);
        if (item is null) return NotFound();
        if (!item.PacienteId.HasValue || !item.EspecialidadeId.HasValue)
        {
            TempData["Erro"] = "Conclua a identificação do paciente e da especialidade antes de buscar horários.";
            return RedirectToAction(nameof(Triagem), new { id });
        }
        if (item.Status != StatusSolicitacaoAtendimento.BuscandoHorario ||
            item.ElegivelConvenio == false ||
            !string.IsNullOrWhiteSpace(item.PendenciaTriagem))
        {
            TempData["Erro"] = item.PendenciaTriagem ?? "Conclua a triagem administrativa antes de buscar horários.";
            return RedirectToAction(nameof(Triagem), new { id });
        }

        await _service.AtualizarStatusAsync(id, StatusSolicitacaoAtendimento.BuscandoHorario, ct);
        var nomeEspecialidade = item.Especialidade?.Nome;
        return RedirectToAction("Create", "Consulta", new
        {
            pacienteId = item.PacienteId,
            medicoId = item.MedicoId,
            especialidade = nomeEspecialidade,
            tipoPagamento = string.Equals(item.ConvenioInformado, "Particular", StringComparison.OrdinalIgnoreCase) ? "Particular" : "Convenio",
            solicitacaoId = item.Id
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AguardarPaciente(int id, CancellationToken ct)
    {
        var item = await _context.SolicitacoesAtendimento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.ConsultaId.HasValue || item.Status is StatusSolicitacaoAtendimento.Confirmada or StatusSolicitacaoAtendimento.Cancelada or StatusSolicitacaoAtendimento.Encerrada)
        {
            TempData["Erro"] = "Esta solicitação já foi finalizada e não pode voltar para aguardando paciente.";
            return RedirectToAction(nameof(Triagem), new { id });
        }

        await _service.AtualizarStatusAsync(id, StatusSolicitacaoAtendimento.AguardandoPaciente, ct);
        TempData["Sucesso"] = "Solicitação marcada como aguardando resposta do paciente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Encerrar(int id, CancellationToken ct)
    {
        var item = await _context.SolicitacoesAtendimento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.ConsultaId.HasValue || item.Status is StatusSolicitacaoAtendimento.Confirmada or StatusSolicitacaoAtendimento.Cancelada or StatusSolicitacaoAtendimento.Encerrada)
        {
            TempData["Erro"] = "Esta solicitação já foi finalizada ou possui consulta vinculada. O histórico não pode ser alterado por esta ação.";
            return RedirectToAction(nameof(Triagem), new { id });
        }

        await _service.AtualizarStatusAsync(id, StatusSolicitacaoAtendimento.Encerrada, ct);
        TempData["Sucesso"] = "Solicitação encerrada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, CancellationToken ct)
    {
        var item = await _context.SolicitacoesAtendimento.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (item.ConsultaId.HasValue || item.Status is StatusSolicitacaoAtendimento.Confirmada or StatusSolicitacaoAtendimento.Cancelada or StatusSolicitacaoAtendimento.Encerrada)
        {
            TempData["Erro"] = "Esta solicitação já foi finalizada ou possui consulta vinculada. Cancele a consulta vinculada quando for necessário.";
            return RedirectToAction(nameof(Triagem), new { id });
        }

        await _service.AtualizarStatusAsync(id, StatusSolicitacaoAtendimento.Cancelada, ct);
        TempData["Sucesso"] = "Solicitação cancelada.";
        return RedirectToAction(nameof(Index));
    }

    private Task<SolicitacaoAtendimento?> CarregarAsync(int id, CancellationToken ct) =>
        _context.SolicitacoesAtendimento
            .Include(x => x.Paciente)
            .Include(x => x.Especialidade)
            .Include(x => x.Medico)
            .Include(x => x.Consulta)
            .Include(x => x.ConversaAtendimento)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task PrepararSeletoresAsync(
        CancellationToken ct,
        int? pacienteId = null,
        int? especialidadeId = null,
        int? medicoId = null)
    {
        ViewBag.Pacientes = new SelectList(
            await _context.Pacientes.AsNoTracking().Where(x => x.Ativo).OrderBy(x => x.Nome).ToListAsync(ct),
            "Id", "Nome", pacienteId);
        ViewBag.Especialidades = new SelectList(
            await _context.Especialidades.AsNoTracking().Where(x => x.Ativo).OrderBy(x => x.Nome).ToListAsync(ct),
            "Id", "Nome", especialidadeId);
        ViewBag.Medicos = new SelectList(
            await _context.Medicos.AsNoTracking().Where(x => x.Ativo).OrderBy(x => x.Nome).ToListAsync(ct),
            "Id", "Nome", medicoId);
    }
}
