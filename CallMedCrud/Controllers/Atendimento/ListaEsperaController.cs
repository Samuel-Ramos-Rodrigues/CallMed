using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.ViewModels;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Paciente,Funcionario,Admin")]
public class ListaEsperaController : Controller
{
    private readonly MKSANContext _context;
    private readonly UsuarioVinculoService _vinculos;
    private readonly ListaEsperaService _service;
    private readonly AgendamentoService _agendamento;
    private readonly AuditoriaService _auditoria;
    private readonly SolicitacaoAtendimentoService _solicitacoes;

    public ListaEsperaController(
        MKSANContext context,
        UsuarioVinculoService vinculos,
        ListaEsperaService service,
        AgendamentoService agendamento,
        AuditoriaService auditoria,
        SolicitacaoAtendimentoService solicitacoes)
    {
        _context = context;
        _vinculos = vinculos;
        _service = service;
        _agendamento = agendamento;
        _auditoria = auditoria;
        _solicitacoes = solicitacoes;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var q = _context.ListasEspera
            .AsNoTracking()
            .Include(x => x.Paciente)
            .Include(x => x.Medico)
            .Include(x => x.Especialidade)
            .AsQueryable();

        if (User.IsInRole("Paciente"))
        {
            var p = await _vinculos.ObterPacienteAsync(User, ct);
            if (p is null) return Forbid();
            q = q.Where(x => x.PacienteId == p.Id);
        }

        var lista = await q
            .OrderByDescending(x => x.Ativa)
            .ThenBy(x => x.CriadoEm)
            .ToListAsync(ct);

        var idsVagas = lista
            .Where(x => x.Ativa && x.UltimaDisponibilidadeId.HasValue)
            .Select(x => x.UltimaDisponibilidadeId!.Value)
            .Distinct()
            .ToArray();

        var vagas = idsVagas.Length == 0
            ? new Dictionary<int, MKSANCrud.Models.Disponibilidade>()
            : await _context.Disponibilidades
                .AsNoTracking()
                .Include(x => x.Medico)
                .Where(x => idsVagas.Contains(x.Id) && x.Ativo && x.Data.HasValue)
                .ToDictionaryAsync(x => x.Id, ct);

        ViewBag.Ofertas = lista
            .Where(x => x.UltimaDisponibilidadeId.HasValue && vagas.ContainsKey(x.UltimaDisponibilidadeId.Value))
            .ToDictionary(x => x.Id, x => vagas[x.UltimaDisponibilidadeId!.Value]);

        return View(lista);
    }

    public async Task<IActionResult> Create(CancellationToken ct) { await PrepararAsync(ct); return View(new ListaEsperaFormViewModel()); }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListaEsperaFormViewModel model, CancellationToken ct)
    {
        int pacienteId;
        if(User.IsInRole("Paciente")) { var p=await _vinculos.ObterPacienteAsync(User,ct); if(p is null) return Forbid(); pacienteId=p.Id; }
        else pacienteId=model.PacienteId.GetValueOrDefault();
        if(pacienteId<=0) ModelState.AddModelError(nameof(model.PacienteId),"Selecione o paciente.");
        if(!model.MedicoId.HasValue && !model.EspecialidadeId.HasValue) ModelState.AddModelError(string.Empty,"Escolha um médico ou uma especialidade.");
        if(model.MedicoId.HasValue && model.EspecialidadeId.HasValue) ModelState.AddModelError(string.Empty,"Escolha um médico específico ou uma especialidade, não os dois.");
        if(!ModelState.IsValid){ await PrepararAsync(ct); return View(model); }
        try { var criado = await _service.AdicionarAsync(pacienteId, model.MedicoId, model.EspecialidadeId, model.DataPreferida, model.Periodo, model.Observacao, ct); await _auditoria.RegistrarAsync("Criar", "Lista de espera", criado.Id, "Paciente incluído na lista de espera.", ct: ct); TempData["Sucesso"]="Você entrou na lista de espera. A CallMed avisará pelo canal preferido configurado, com alternativas de contingência quando disponíveis, quando surgir uma vaga compatível."; return RedirectToAction(nameof(Index)); }
        catch(InvalidOperationException ex){ ModelState.AddModelError(string.Empty,ex.Message); await PrepararAsync(ct); return View(model); }
    }

    [HttpPost][ValidateAntiForgeryToken]
    [Authorize(Roles = "Paciente")]
    public async Task<IActionResult> AceitarVaga(int id, string? returnUrl, CancellationToken ct)
    {
        var paciente = await _vinculos.ObterPacienteAsync(User, ct);
        if (paciente is null) return Forbid();

        var item = await _context.ListasEspera
            .Include(x => x.Especialidade)
            .FirstOrDefaultAsync(x => x.Id == id && x.Ativa && x.PacienteId == paciente.Id, ct);
        if (item is null) return NotFound();
        if (!item.UltimaDisponibilidadeId.HasValue)
        {
            TempData["Erro"] = "Essa lista ainda não possui uma vaga disponível.";
            return RedirectToAction(nameof(Index));
        }

        var vaga = await _context.Disponibilidades.AsNoTracking()
            .Include(x => x.Medico)
            .FirstOrDefaultAsync(x => x.Id == item.UltimaDisponibilidadeId.Value && x.Ativo && x.Data.HasValue, ct);
        if (vaga is null || !vaga.Data.HasValue)
        {
            TempData["Erro"] = "A vaga não está mais disponível. A CallMed continuará procurando outra opção.";
            item.UltimaDisponibilidadeId = null; item.AtualizadoEm = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }

        var resultado = await _agendamento.AgendarAsync(
            paciente.Id, vaga.MedicoId, vaga.Data.Value, vaga.Horario,
            $"Agendamento aceito pela lista de espera #{item.Id}.",
            ct: ct);

        if (!resultado.Sucesso)
        {
            TempData["Erro"] = resultado.Mensagem + " A CallMed continuará procurando outra vaga.";
            item.UltimaDisponibilidadeId = null; item.AtualizadoEm = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            item.Ativa = false; item.AtualizadoEm = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            if (resultado.Consulta is not null)
            {
                var solicitacao = await _solicitacoes.CriarAsync(
                    CanalAtendimento.Web,
                    paciente.Id,
                    vaga.Medico?.EspecialidadeId ?? item.EspecialidadeId,
                    vaga.MedicoId,
                    vaga.Data.Value,
                    item.Periodo,
                    $"Vaga aceita pela lista de espera #{item.Id}.",
                    ct: ct);
                await _solicitacoes.VincularConsultaAsync(solicitacao.Id, resultado.Consulta.Id, ct);
            }

            await _auditoria.RegistrarAsync("Aceitar vaga", "Lista de espera", item.Id, $"Vaga aceita e convertida na consulta #{resultado.Consulta?.Id}.", ct: ct);
            TempData["Sucesso"] = "Vaga aceita e consulta agendada com sucesso.";
        }

        if(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost][ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, string? returnUrl, CancellationToken ct)
    {
        var item=await _context.ListasEspera.FirstOrDefaultAsync(x=>x.Id==id,ct); if(item is null) return NotFound();
        if(User.IsInRole("Paciente")){ var p=await _vinculos.ObterPacienteAsync(User,ct); if(p is null || item.PacienteId!=p.Id) return Forbid(); }
        item.Ativa=false; item.AtualizadoEm=DateTime.UtcNow; await _context.SaveChangesAsync(ct); await _auditoria.RegistrarAsync("Cancelar", "Lista de espera", item.Id, "Pedido de lista de espera cancelado.", ct: ct); TempData["Sucesso"]="Lista de espera cancelada."; if(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl); return RedirectToAction(nameof(Index));
    }

    private async Task PrepararAsync(CancellationToken ct)
    {
        ViewBag.Medicos = new SelectList(await _context.Medicos.AsNoTracking().Where(m=>m.Ativo).OrderBy(m=>m.Nome).ToListAsync(ct),"Id","Nome");
        ViewBag.Especialidades = new SelectList(await _context.Especialidades.AsNoTracking().Where(e=>e.Ativo && e.Medicos.Any(m=>m.Ativo)).OrderBy(e=>e.Nome).ToListAsync(ct),"Id","Nome");
        if(!User.IsInRole("Paciente")) ViewBag.Pacientes = new SelectList(await _context.Pacientes.AsNoTracking().Where(p=>p.Ativo).OrderBy(p=>p.Nome).ToListAsync(ct),"Id","Nome");
    }
}
