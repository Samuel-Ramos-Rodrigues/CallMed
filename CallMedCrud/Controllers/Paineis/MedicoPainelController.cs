using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Medico")]
public class MedicoPainelController : Controller
{
    private readonly MKSANContext _context;
    private readonly UsuarioVinculoService _vinculos;
    private readonly IClinicaClock _clock;
    private readonly AgendamentoService _agendamento;
    private readonly AgendaMedicoService _agendaMedico;

    public MedicoPainelController(MKSANContext context, UsuarioVinculoService vinculos, IClinicaClock clock, AgendamentoService agendamento, AgendaMedicoService agendaMedico)
    { _context = context; _vinculos = vinculos; _clock = clock; _agendamento = agendamento; _agendaMedico = agendaMedico; }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var medico = await _vinculos.ObterMedicoAsync(User, ct);
        if (medico is null || !medico.Ativo) return Forbid();

        var hoje = _clock.Hoje;
        var fimSemana = hoje.AddDays(7);
        var consultasBase = _context.Consultas.AsNoTracking().Include(c => c.Paciente)
            .Where(c => c.MedicoId == medico.Id && c.Status != ConsultaStatus.Cancelada);

        var consultasHoje = await consultasBase.CountAsync(c => c.Data.Date == hoje, ct);
        var slotsHoje = await _context.Disponibilidades.CountAsync(d => d.MedicoId == medico.Id && d.Ativo && d.Data.HasValue && d.Data.Value.Date == hoje, ct);

        var candidatas = await _context.Disponibilidades.AsNoTracking()
            .Where(d => d.MedicoId == medico.Id && d.Ativo && d.Data.HasValue && d.Data.Value.Date >= hoje && d.Data.Value.Date <= hoje.AddDays(14))
            .OrderBy(d => d.Data).ThenBy(d => d.Horario).Take(80).ToListAsync(ct);
        var consultasSlots = await _context.Consultas.AsNoTracking()
            .Where(c => c.MedicoId == medico.Id && c.Data.Date >= hoje && c.Data.Date <= hoje.AddDays(14) && c.Status != ConsultaStatus.Cancelada)
            .Select(c => new { c.Data, c.Horario }).ToListAsync(ct);
        var ocupados = consultasSlots.Select(c => $"{c.Data:yyyy-MM-dd}|{c.Horario}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vagasLivres = candidatas.Where(v => v.Data.HasValue && !ocupados.Contains($"{v.Data.Value:yyyy-MM-dd}|{v.Horario}")).Take(12).ToList();

        var model = new MedicoPainelViewModel
        {
            Medico = medico,
            ConsultasHoje = consultasHoje,
            ConsultasSemana = await consultasBase.CountAsync(c => c.Data.Date >= hoje && c.Data.Date < fimSemana, ct),
            PacientesHoje = await consultasBase.Where(c => c.Data.Date == hoje).Select(c => c.PacienteId).Distinct().CountAsync(ct),
            VagasHoje = Math.Max(0, slotsHoje - consultasHoje),
            AgendaHoje = await consultasBase.Where(c => c.Data.Date == hoje).OrderBy(c => c.Horario).ToListAsync(ct),
            ProximasConsultas = await consultasBase.Where(c => c.Data.Date >= hoje).OrderBy(c => c.Data).ThenBy(c => c.Horario).Take(20).ToListAsync(ct),
            AgendaSemanal = await _context.MedicoHorariosSemanais.AsNoTracking().Where(h => h.MedicoId == medico.Id && h.Ativo).OrderBy(h => h.DiaSemana).ThenBy(h => h.Horario).ToListAsync(ct),
            ProximasVagasLivres = vagasLivres,
            BloqueiosManuais = await _context.Disponibilidades.AsNoTracking()
                .Where(d => d.MedicoId == medico.Id && d.BloqueioManual && d.Data.HasValue && d.Data.Value.Date >= hoje)
                .OrderBy(d => d.Data).ThenBy(d => d.Horario).Take(12).ToListAsync(ct)
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirmar(int consultaId, CancellationToken ct)
    {
        var medico = await _vinculos.ObterMedicoAsync(User, ct);
        if (medico is null || !await _context.Consultas.AnyAsync(c => c.Id == consultaId && c.MedicoId == medico.Id, ct)) return Forbid();
        var r = await _agendamento.ConfirmarAsync(consultaId, ct);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Concluir(int consultaId, CancellationToken ct)
    {
        var medico = await _vinculos.ObterMedicoAsync(User, ct);
        if (medico is null || !await _context.Consultas.AnyAsync(c => c.Id == consultaId && c.MedicoId == medico.Id, ct)) return Forbid();
        var r = await _agendamento.RealizarAsync(consultaId, ct);
        TempData[r.Sucesso ? "Sucesso" : "Erro"] = r.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BloquearHorario(int disponibilidadeId, CancellationToken ct)
    {
        var medico = await _vinculos.ObterMedicoAsync(User, ct);
        if (medico is null || !medico.Ativo) return Forbid();
        var vaga = await _context.Disponibilidades.FirstOrDefaultAsync(d => d.Id == disponibilidadeId && d.MedicoId == medico.Id, ct);
        if (vaga is null) return NotFound();
        var ocupada = vaga.Data.HasValue && await _context.Consultas.AnyAsync(c => c.MedicoId == medico.Id && c.Data.Date == vaga.Data.Value.Date && c.Horario == vaga.Horario && c.Status != ConsultaStatus.Cancelada, ct);
        if (ocupada) TempData["Erro"] = "Esse horário já possui uma consulta e não pode ser bloqueado.";
        else { vaga.BloqueioManual = true; vaga.Ativo = false; await _context.SaveChangesAsync(ct); TempData["Sucesso"] = "Horário bloqueado. Ele permanecerá fechado mesmo após a renovação automática da agenda."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesbloquearHorario(int disponibilidadeId, CancellationToken ct)
    {
        var medico = await _vinculos.ObterMedicoAsync(User, ct);
        if (medico is null || !medico.Ativo) return Forbid();
        var vaga = await _context.Disponibilidades.FirstOrDefaultAsync(d => d.Id == disponibilidadeId && d.MedicoId == medico.Id, ct);
        if (vaga is null) return NotFound();

        vaga.BloqueioManual = false;
        await _context.SaveChangesAsync(ct);
        await _agendaMedico.SincronizarDisponibilidadesFuturasAsync(medico.Id, 120, ct);
        TempData["Sucesso"] = "Horário desbloqueado. Se ele fizer parte da sua agenda semanal, voltou a ficar disponível.";
        return RedirectToAction(nameof(Index));
    }

}
