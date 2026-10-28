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

    public SolicitacoesController(
        MKSANContext context,
        SolicitacaoAtendimentoService service)
    {
        _context = context;
        _service = service;
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
        ViewData["Subtitle"] = "Organize o pedido e escolha uma vaga. A consulta fica confirmada assim que o agendamento é concluído.";
        return View(new SolicitacoesPainelViewModel { Itens = itens, Busca = busca, Canal = canal });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Contingencia() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> ImportarContingencia(
        [FromBody] ContingenciaImportacaoViewModel model, CancellationToken ct)
    {
        if (model is null) return BadRequest(new { mensagem = "Pedido inválido." });
        if (model.Chave == Guid.Empty)
            ModelState.AddModelError(nameof(model.Chave), "Protocolo inválido.");
        if (string.IsNullOrWhiteSpace(model.Nome) || string.IsNullOrWhiteSpace(model.Especialidade))
            ModelState.AddModelError(string.Empty, "Informe o nome e a especialidade desejada.");
        if (model.CapturadaEm == default || model.CapturadaEm > DateTimeOffset.UtcNow.AddMinutes(5))
            ModelState.AddModelError(nameof(model.CapturadaEm), "Confira a data e a hora do dispositivo.");
        if (!ModelState.IsValid)
            return BadRequest(new { mensagem = "Revise o pedido.", erros = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage) });

        // A mesma chave nunca cria duas solicitações, mesmo em abas simultâneas ou
        // quando a resposta se perde depois do commit. O índice é a segunda proteção.
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        var chave = model.Chave.ToString("D");
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({chave}, 22))", ct);
        var existente = await _context.SolicitacoesAtendimento.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChaveContingencia == model.Chave, ct);
        if (existente is not null)
        {
            await tx.CommitAsync(ct);
            return Ok(new { id = existente.Id, chave = model.Chave, mensagem = "Pedido já recebido anteriormente." });
        }

        var capturada = model.CapturadaEm.UtcDateTime;
        if (capturada > DateTime.UtcNow) capturada = DateTime.UtcNow;
        var item = await _service.CriarAsync(
            model.Canal == "Telefone" ? CanalAtendimento.Telefone : CanalAtendimento.Presencial,
            null, null, null, model.DataPreferida, model.Periodo,
            $"Pedido registrado durante contingência. Especialidade solicitada: {model.Especialidade.Trim()}. Confirmar os dados de contato e escolher uma vaga para agendar.",
            model.Nome.Trim(), model.Telefone, model.Email, ct: ct,
            chaveContingencia: model.Chave, capturadaEm: capturada);
        await tx.CommitAsync(ct);
        return Ok(new { id = item.Id, chave = model.Chave, mensagem = "Pedido recebido. Nenhuma vaga foi reservada até a escolha de um horário." });
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
            await PrepararSeletoresAsync(ct, model.PacienteId, model.EspecialidadeId, model.MedicoId);
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
            return RedirectToAction(nameof(Detalhes), new { id = item.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PrepararSeletoresAsync(ct, model.PacienteId, model.EspecialidadeId, model.MedicoId);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct)
    {
        var item = await CarregarAsync(id, ct);
        if (item is null) return NotFound();

        await PrepararSeletoresAsync(ct, item.PacienteId, item.EspecialidadeId, item.MedicoId);
        var historico = await _context.AuditoriaEventos
            .AsNoTracking()
            .Where(x => x.Entidade == "Solicitação" && x.EntidadeId == id.ToString())
            .OrderByDescending(x => x.CriadoEm)
            .Take(30)
            .ToListAsync(ct);

        ViewData["Title"] = $"Solicitação #{id}";
        ViewData["Subtitle"] = "Confira os dados básicos e siga diretamente para a escolha de horário.";
        return View(new SolicitacaoDetalhesViewModel { Solicitacao = item, Historico = historico });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepararAgendamento(
        int id,
        int? pacienteId,
        int? especialidadeId,
        int? medicoId,
        DateTime? dataPreferida,
        string? periodoPreferido,
        string? observacao,
        CancellationToken ct)
    {
        var responsavel = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var resultado = await _service.PrepararAgendamentoAsync(
            id, pacienteId, especialidadeId, medicoId, dataPreferida,
            periodoPreferido, observacao, responsavel, ct);

        if (!resultado.Sucesso || resultado.Item is null)
        {
            TempData["Erro"] = resultado.Mensagem;
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        var item = resultado.Item;
        var nomeEspecialidade = await _context.Especialidades.AsNoTracking()
            .Where(x => x.Id == item.EspecialidadeId)
            .Select(x => x.Nome)
            .FirstOrDefaultAsync(ct);
        var tipoPagamento = item.Paciente?.TemConvenio == true ? "Convenio" : "Particular";
        TempData["Sucesso"] = resultado.Mensagem;
        return RedirectToAction("Create", "Consulta", new
        {
            pacienteId = item.PacienteId,
            medicoId = item.MedicoId,
            data = item.DataPreferida,
            especialidade = nomeEspecialidade,
            tipoPagamento,
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
            return RedirectToAction(nameof(Detalhes), new { id });
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
            return RedirectToAction(nameof(Detalhes), new { id });
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
            return RedirectToAction(nameof(Detalhes), new { id });
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
        var medicos = await _context.Medicos.AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new MedicoOpcaoViewModel
            {
                Id = x.Id,
                Nome = x.Nome,
                EspecialidadeId = x.EspecialidadeId
            })
            .ToListAsync(ct);
        ViewBag.MedicosOpcoes = medicos;
        var medicosSelect = especialidadeId.HasValue
            ? medicos.Where(x => x.EspecialidadeId == especialidadeId.Value).ToList()
            : medicos;
        ViewBag.Medicos = new SelectList(medicosSelect, "Id", "Nome", medicoId);
    }
}
