using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed class DisponibilidadeController : Controller
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;
    private readonly AgendaMedicoService _agenda;

    public DisponibilidadeController(
        MKSANContext context,
        IClinicaClock clock,
        AgendaMedicoService agenda)
    {
        _context = context;
        _clock = clock;
        _agenda = agenda;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var hoje = _clock.Hoje;
        var model = new AgendaExcecoesPageViewModel
        {
            Excecoes = await _context.AgendaExcecoes
                .AsNoTracking()
                .Include(x => x.Medico)
                .Where(x => x.Data.Date >= hoje)
                .OrderByDescending(x => x.Ativa)
                .ThenBy(x => x.Data)
                .ThenBy(x => x.HorarioInicio)
                .Take(250)
                .ToListAsync(ct),
            BloqueiosDoMedico = await _context.Disponibilidades
                .AsNoTracking()
                .Include(d => d.Medico)
                .Where(d =>
                    d.BloqueioManual &&
                    d.AgendaExcecaoId == null &&
                    d.Data.HasValue &&
                    d.Data.Value.Date >= hoje)
                .OrderBy(d => d.Data)
                .ThenBy(d => d.Horario)
                .Take(100)
                .ToListAsync(ct)
        };

        return View(model);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await CarregarMedicos(null, ct);
        return View(new AgendaExcecaoFormViewModel
        {
            Data = _clock.Hoje,
            Tipo = AgendaExcecaoTipo.Bloqueio
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AgendaExcecaoFormViewModel model,
        CancellationToken ct)
    {
        Normalizar(model);
        await ValidarAsync(model, ct);

        if (!ModelState.IsValid)
        {
            await CarregarMedicos(model.MedicoId, ct);
            return View(model);
        }

        var excecao = new AgendaExcecao
        {
            MedicoId = model.MedicoId,
            Tipo = model.Tipo,
            Data = model.Data!.Value.Date,
            HorarioInicio = model.HorarioInicio,
            HorarioFim = model.HorarioFim,
            Motivo = model.Motivo,
            Ativa = true,
            CriadoEm = DateTime.UtcNow
        };

        _context.AgendaExcecoes.Add(excecao);
        await _context.SaveChangesAsync(ct);
        await _agenda.SincronizarDisponibilidadesFuturasAsync(model.MedicoId, 120, ct);

        TempData["Sucesso"] = model.Tipo switch
        {
            AgendaExcecaoTipo.Encaixe => "Encaixe extra aberto com sucesso.",
            AgendaExcecaoTipo.Ausencia => "Ausência registrada. Os horários do dia foram bloqueados.",
            _ => "Bloqueio de agenda registrado com sucesso."
        };

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id, CancellationToken ct)
    {
        var excecao = await _context.AgendaExcecoes
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (excecao is null)
            return NotFound();

        if (!excecao.Ativa)
            return RedirectToAction(nameof(Index));

        if (excecao.Tipo == AgendaExcecaoTipo.Encaixe &&
            !string.IsNullOrWhiteSpace(excecao.HorarioInicio) &&
            await ExisteConsultaNoIntervaloAsync(
                excecao.MedicoId,
                excecao.Data,
                excecao.HorarioInicio,
                excecao.HorarioInicio,
                ct))
        {
            TempData["Erro"] = "Esse encaixe já possui uma consulta. Remarque ou cancele a consulta antes de remover a exceção.";
            return RedirectToAction(nameof(Index));
        }

        var slots = await _context.Disponibilidades
            .Where(d => d.AgendaExcecaoId == excecao.Id)
            .ToListAsync(ct);

        foreach (var slot in slots)
        {
            if (excecao.Tipo == AgendaExcecaoTipo.Encaixe && !slot.OrigemAgendaSemanal)
            {
                _context.Disponibilidades.Remove(slot);
                continue;
            }

            slot.AgendaExcecaoId = null;
            slot.BloqueioManual = false;
        }

        excecao.Ativa = false;
        excecao.EncerradoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _agenda.SincronizarDisponibilidadesFuturasAsync(excecao.MedicoId, 120, ct);

        TempData["Sucesso"] = "Exceção encerrada e agenda recalculada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesbloquearMedico(int disponibilidadeId, CancellationToken ct)
    {
        var slot = await _context.Disponibilidades
            .FirstOrDefaultAsync(d =>
                d.Id == disponibilidadeId &&
                d.BloqueioManual &&
                d.AgendaExcecaoId == null,
                ct);

        if (slot is null)
            return NotFound();

        slot.BloqueioManual = false;
        await _context.SaveChangesAsync(ct);
        await _agenda.SincronizarDisponibilidadesFuturasAsync(slot.MedicoId, 120, ct);
        TempData["Sucesso"] = "Bloqueio do médico removido.";
        return RedirectToAction(nameof(Index));
    }

    // Rotas antigas ficam redirecionadas para a nova experiência de exceções.
    public IActionResult Details(int? id) => RedirectToAction(nameof(Index));
    public IActionResult Edit(int? id) => RedirectToAction(nameof(Index));
    public IActionResult Delete(int? id) => RedirectToAction(nameof(Index));

    private static void Normalizar(AgendaExcecaoFormViewModel model)
    {
        model.Tipo = model.Tipo?.Trim() ?? string.Empty;
        model.Motivo = string.IsNullOrWhiteSpace(model.Motivo) ? null : model.Motivo.Trim();
        model.HorarioInicio = NormalizarHorario(model.HorarioInicio);
        model.HorarioFim = NormalizarHorario(model.HorarioFim);

        if (model.Tipo == AgendaExcecaoTipo.Ausencia)
        {
            model.HorarioInicio = null;
            model.HorarioFim = null;
        }

        if (model.Tipo == AgendaExcecaoTipo.Encaixe)
        {
            model.HorarioFim = null;
        }
        else if (model.Tipo == AgendaExcecaoTipo.Bloqueio &&
                 TimeSpan.TryParse(model.HorarioInicio, out var inicio) &&
                 TimeSpan.TryParse(model.HorarioFim, out var fim) &&
                 fim < inicio)
        {
            (model.HorarioInicio, model.HorarioFim) = (model.HorarioFim, model.HorarioInicio);
        }
    }

    private async Task ValidarAsync(AgendaExcecaoFormViewModel model, CancellationToken ct)
    {
        if (!AgendaExcecaoTipo.Todos.Contains(model.Tipo, StringComparer.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.Tipo), "Tipo de exceção inválido.");

        if (!model.Data.HasValue)
            return;

        var data = model.Data.Value.Date;
        if (data < _clock.Hoje)
            ModelState.AddModelError(nameof(model.Data), "A data não pode estar no passado.");

        if (!await _context.Medicos.AnyAsync(m => m.Id == model.MedicoId && m.Ativo, ct))
            ModelState.AddModelError(nameof(model.MedicoId), "Selecione um médico ativo.");

        if (model.Tipo is AgendaExcecaoTipo.Bloqueio or AgendaExcecaoTipo.Encaixe)
        {
            if (!HorarioValido(model.HorarioInicio))
                ModelState.AddModelError(nameof(model.HorarioInicio), "Informe um horário válido.");
        }

        if (model.Tipo == AgendaExcecaoTipo.Bloqueio &&
            !string.IsNullOrWhiteSpace(model.HorarioFim) &&
            !HorarioValido(model.HorarioFim))
        {
            ModelState.AddModelError(nameof(model.HorarioFim), "Informe um horário final válido.");
        }

        if (!ModelState.IsValid)
            return;

        var inicio = model.HorarioInicio;
        var fim = string.IsNullOrWhiteSpace(model.HorarioFim) ? inicio : model.HorarioFim;

        if (model.Tipo == AgendaExcecaoTipo.Ausencia)
        {
            var consulta = await _context.Consultas
                .AsNoTracking()
                .Where(c =>
                    c.MedicoId == model.MedicoId &&
                    c.Data.Date == data &&
                    c.Status != ConsultaStatus.Cancelada)
                .OrderBy(c => c.Horario)
                .FirstOrDefaultAsync(ct);

            if (consulta is not null)
                ModelState.AddModelError(string.Empty, $"Há consulta marcada em {data:dd/MM/yyyy} às {consulta.Horario}. Remarque ou cancele antes de registrar a ausência.");
        }
        else if (await ExisteConsultaNoIntervaloAsync(model.MedicoId, data, inicio!, fim!, ct))
        {
            ModelState.AddModelError(string.Empty, "Existe consulta marcada no horário informado. Remarque ou cancele a consulta antes de alterar a agenda.");
        }

        var excecoesDia = await _context.AgendaExcecoes
            .AsNoTracking()
            .Where(x => x.Ativa && x.MedicoId == model.MedicoId && x.Data.Date == data)
            .ToListAsync(ct);

        if (excecoesDia.Any(x => x.Tipo == AgendaExcecaoTipo.Ausencia) ||
            (model.Tipo == AgendaExcecaoTipo.Ausencia && excecoesDia.Count > 0))
        {
            ModelState.AddModelError(string.Empty, "Já existe uma exceção que cobre esse dia. Encerre a exceção atual antes de criar outra.");
        }

        if (model.Tipo == AgendaExcecaoTipo.Encaixe && HorarioValido(inicio))
        {
            var jaDisponivel = await _context.Disponibilidades.AnyAsync(d =>
                d.MedicoId == model.MedicoId &&
                d.Data.HasValue &&
                d.Data.Value.Date == data &&
                d.Horario == inicio &&
                d.Ativo,
                ct);

            if (jaDisponivel)
            {
                ModelState.AddModelError(string.Empty, "Esse horário já está disponível; não é necessário abrir um encaixe.");
            }
            else
            {
                var bloqueioManual = await _context.Disponibilidades.AnyAsync(d =>
                    d.MedicoId == model.MedicoId &&
                    d.Data.HasValue &&
                    d.Data.Value.Date == data &&
                    d.Horario == inicio &&
                    d.BloqueioManual &&
                    d.AgendaExcecaoId == null,
                    ct);

                if (bloqueioManual)
                    ModelState.AddModelError(string.Empty, "Esse horário foi bloqueado pelo médico. Desbloqueie-o antes de abrir um encaixe no mesmo horário.");
            }
        }

        var duplicada = excecoesDia.Any(x =>
            x.Tipo == model.Tipo &&
            string.Equals(x.HorarioInicio ?? string.Empty, inicio ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.HorarioFim ?? string.Empty, model.HorarioFim ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (duplicada)
            ModelState.AddModelError(string.Empty, "Já existe uma exceção igual para esse médico e data.");
    }

    private async Task<bool> ExisteConsultaNoIntervaloAsync(
        int medicoId,
        DateTime data,
        string inicio,
        string fim,
        CancellationToken ct)
    {
        if (!TimeSpan.TryParse(inicio, out var horaInicio) || !TimeSpan.TryParse(fim, out var horaFim))
            return false;

        if (horaFim < horaInicio)
            (horaInicio, horaFim) = (horaFim, horaInicio);

        var horarios = await _context.Consultas
            .AsNoTracking()
            .Where(c =>
                c.MedicoId == medicoId &&
                c.Data.Date == data.Date &&
                c.Status != ConsultaStatus.Cancelada)
            .Select(c => c.Horario)
            .ToListAsync(ct);

        return horarios.Any(h =>
            TimeSpan.TryParse(h, out var hora) &&
            hora >= horaInicio &&
            hora <= horaFim);
    }

    private async Task CarregarMedicos(int? selecionado, CancellationToken ct)
    {
        ViewBag.MedicoId = new SelectList(
            await _context.Medicos
                .AsNoTracking()
                .Where(m => m.Ativo)
                .OrderBy(m => m.Nome)
                .ToListAsync(ct),
            "Id",
            "Nome",
            selecionado);
    }

    private static bool HorarioValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) &&
        TimeOnly.TryParseExact(
            valor,
            ["HH:mm", "H:mm"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);

    private static string? NormalizarHorario(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        return TimeOnly.TryParse(valor, out var hora)
            ? hora.ToString("HH:mm", CultureInfo.InvariantCulture)
            : valor.Trim();
    }
}
