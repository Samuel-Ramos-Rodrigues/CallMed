using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public class MedicoController : Controller
{
    private readonly MKSANContext _context;
    private readonly EspecialidadeService _especialidades;
    private readonly AgendaMedicoService _agenda;
    private readonly IClinicaClock _clock;

    public MedicoController(
        MKSANContext context,
        EspecialidadeService especialidades,
        AgendaMedicoService agenda,
        IClinicaClock clock)
    {
        _context = context;
        _especialidades = especialidades;
        _agenda = agenda;
        _clock = clock;
    }

    public async Task<IActionResult> Index() =>
        View(await _context.Medicos
            .AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .OrderBy(m => m.Nome)
            .ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Medicos
            .AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .Include(m => m.HorariosSemanais)
            .FirstOrDefaultAsync(m => m.Id == id);

        return item is null ? NotFound() : View(item);
    }

    public IActionResult Create()
    {
        PrepararHorarios();
        return View(new MedicoFormViewModel
        {
            Ativo = true,
            Agenda = AgendaDiaViewModel.CriarSemana()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MedicoFormViewModel model)
    {
        Normalizar(model);
        ValidarAgenda(model);
        await ValidarCrmAsync(model.Crm, null);

        if (!ModelState.IsValid)
        {
            PrepararHorarios();
            return View(model);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // A especialidade só é criada depois de todas as validações do formulário.
            // Como está na mesma transação, nenhum cadastro parcial fica no Neon.
            var especialidade = await _especialidades.ObterOuCriarAsync(model.Especialidade);

            var medico = new Medico
            {
                Nome = model.Nome,
                EspecialidadeId = especialidade.Id,
                Especialidade = especialidade.Nome,
                Crm = model.Crm,
                Ativo = model.Ativo
            };

            _context.Medicos.Add(medico);
            await _context.SaveChangesAsync();
            await _agenda.SalvarAgendaAsync(medico.Id, model.Agenda);

            await transaction.CommitAsync();

            TempData["Sucesso"] = "Médico cadastrado com especialidade e agenda semanal.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(
                string.Empty,
                "Não foi possível cadastrar o médico. Verifique CRM, especialidade e horários informados.");
            PrepararHorarios();
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Medicos
            .AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (item is null)
            return NotFound();

        PrepararHorarios();

        return View(new MedicoFormViewModel
        {
            Id = item.Id,
            Nome = item.Nome,
            Especialidade = item.EspecialidadeCadastro?.Nome ?? item.Especialidade,
            Crm = item.Crm,
            Ativo = item.Ativo,
            Agenda = await _agenda.ObterAgendaAsync(item.Id)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MedicoFormViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        Normalizar(model);
        ValidarAgenda(model);
        await ValidarCrmAsync(model.Crm, id);

        var atual = await _context.Medicos.FirstOrDefaultAsync(m => m.Id == id);
        if (atual is null)
            return NotFound();

        if (!model.Ativo && atual.Ativo)
        {
            var possuiConsultasFuturas = await _context.Consultas.AnyAsync(c =>
                c.MedicoId == id &&
                c.Data.Date >= _clock.Hoje &&
                c.Status != ConsultaStatus.Cancelada);

            if (possuiConsultasFuturas)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "O médico possui consultas futuras. Remarque ou cancele essas consultas antes de desativá-lo.");
            }
        }

        // Impede retirar um dia/horário que já tenha paciente marcado.
        if (model.Ativo)
        {
            var conflitos = await _agenda.ObterConsultasFuturasForaDaAgendaAsync(id, model.Agenda);
            if (conflitos.Count > 0)
            {
                var primeira = conflitos[0];
                var complemento = conflitos.Count > 1
                    ? $" e mais {conflitos.Count - 1} consulta(s)"
                    : string.Empty;

                ModelState.AddModelError(
                    string.Empty,
                    $"A nova agenda remove horários com consultas marcadas. Ex.: {primeira.Data:dd/MM/yyyy} às {primeira.Horario}{complemento}. Remarque ou cancele antes de alterar a agenda.");
            }
        }

        if (!ModelState.IsValid)
        {
            PrepararHorarios();
            return View(model);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var especialidade = await _especialidades.ObterOuCriarAsync(model.Especialidade);

            atual.Nome = model.Nome;
            atual.EspecialidadeId = especialidade.Id;
            atual.Especialidade = especialidade.Nome;
            atual.Crm = model.Crm;
            atual.Ativo = model.Ativo;

            if (!model.Ativo)
            {
                var listasDiretas = await _context.ListasEspera
                    .Where(x => x.Ativa && x.MedicoId == atual.Id)
                    .ToListAsync();
                foreach (var lista in listasDiretas)
                {
                    lista.Ativa = false;
                    lista.AtualizadoEm = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            await _agenda.SalvarAgendaAsync(atual.Id, model.Agenda);
            await _especialidades.RemoverOrfasAsync();

            await transaction.CommitAsync();

            TempData["Sucesso"] = "Médico e agenda atualizados com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Não foi possível atualizar o médico.");
            PrepararHorarios();
            return View(model);
        }
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Medicos
            .AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .FirstOrDefaultAsync(m => m.Id == id);

        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await _context.Consultas.AnyAsync(c => c.MedicoId == id))
        {
            TempData["Erro"] = "Não é possível excluir um médico que possui histórico de consultas. Desative o médico em vez disso.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var item = await _context.Medicos.FindAsync(id);
        if (item is not null)
        {
            var listas = await _context.ListasEspera.Where(x => x.Ativa && x.MedicoId == id).ToListAsync();
            foreach (var lista in listas)
            {
                lista.Ativa = false;
                lista.AtualizadoEm = DateTime.UtcNow;
            }
            _context.Medicos.Remove(item);
        }

        await _context.SaveChangesAsync();
        await _especialidades.RemoverOrfasAsync();
        await transaction.CommitAsync();

        TempData["Sucesso"] = "Médico removido.";
        return RedirectToAction(nameof(Index));
    }

    private static void Normalizar(MedicoFormViewModel model)
    {
        model.Nome = model.Nome?.Trim() ?? string.Empty;
        model.Especialidade = model.Especialidade?.Trim() ?? string.Empty;
        model.Crm = string.IsNullOrWhiteSpace(model.Crm)
            ? null
            : model.Crm.Trim().ToUpperInvariant();

        model.Agenda ??= AgendaDiaViewModel.CriarSemana();
    }

    private void ValidarAgenda(MedicoFormViewModel model)
    {
        foreach (var dia in model.Agenda.Where(d => d.Trabalha))
        {
            dia.Horarios ??= [];
            if (dia.Horarios.Count == 0)
            {
                ModelState.AddModelError(
                    $"Agenda[{model.Agenda.IndexOf(dia)}].Horarios",
                    $"Selecione ao menos um horário para {dia.Nome}.");
            }
        }
    }

    private async Task ValidarCrmAsync(string? crm, int? ignorarId)
    {
        if (!string.IsNullOrWhiteSpace(crm) &&
            await _context.Medicos.AnyAsync(m =>
                (!ignorarId.HasValue || m.Id != ignorarId.Value) &&
                m.Crm != null &&
                m.Crm.ToUpper() == crm.ToUpper()))
        {
            ModelState.AddModelError(nameof(MedicoFormViewModel.Crm), "Já existe um médico cadastrado com esse CRM.");
        }
    }

    private void PrepararHorarios()
    {
        ViewBag.HorariosPadrao = AgendaMedicoService.HorariosPadrao;
    }
}
