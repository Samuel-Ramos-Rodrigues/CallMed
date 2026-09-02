using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed class AgendaController : Controller
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;

    public AgendaController(MKSANContext context, IClinicaClock clock)
    {
        _context = context;
        _clock = clock;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? data,
        string? modo,
        int? medicoId,
        int? especialidadeId,
        CancellationToken ct)
    {
        var referencia = (data ?? _clock.Hoje).Date;
        var modoNormalizado = (modo ?? "semana").Trim().ToLowerInvariant();
        if (modoNormalizado is not ("dia" or "semana" or "lista"))
            modoNormalizado = "semana";

        DateTime inicio;
        DateTime fim;

        if (modoNormalizado == "dia")
        {
            inicio = referencia;
            fim = referencia;
        }
        else if (modoNormalizado == "lista")
        {
            inicio = referencia;
            fim = referencia.AddDays(13);
        }
        else
        {
            var deslocamento = ((int)referencia.DayOfWeek + 6) % 7;
            inicio = referencia.AddDays(-deslocamento);
            fim = inicio.AddDays(6);
        }

        var medicosQuery = _context.Medicos
            .AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .Where(m => m.Ativo)
            .AsQueryable();

        if (medicoId.HasValue)
            medicosQuery = medicosQuery.Where(m => m.Id == medicoId.Value);

        if (especialidadeId.HasValue)
            medicosQuery = medicosQuery.Where(m => m.EspecialidadeId == especialidadeId.Value);

        var medicosFiltrados = await medicosQuery
            .OrderBy(m => m.Nome)
            .ToListAsync(ct);

        var ids = medicosFiltrados.Select(m => m.Id).ToArray();

        var disponibilidades = ids.Length == 0
            ? new List<Disponibilidade>()
            : await _context.Disponibilidades
                .AsNoTracking()
                .Include(d => d.Medico)!
                    .ThenInclude(m => m.EspecialidadeCadastro)
                .Include(d => d.AgendaExcecao)
                .Where(d =>
                    ids.Contains(d.MedicoId) &&
                    d.Data.HasValue &&
                    d.Data.Value.Date >= inicio &&
                    d.Data.Value.Date <= fim)
                .OrderBy(d => d.Data)
                .ThenBy(d => d.Horario)
                .ThenBy(d => d.Medico!.Nome)
                .ToListAsync(ct);

        var consultas = ids.Length == 0
            ? new List<Consulta>()
            : await _context.Consultas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.Medico)!
                    .ThenInclude(m => m.EspecialidadeCadastro)
                .Where(c =>
                    ids.Contains(c.MedicoId) &&
                    c.Data.Date >= inicio &&
                    c.Data.Date <= fim &&
                    c.Status != ConsultaStatus.Cancelada)
                .OrderBy(c => c.Data)
                .ThenBy(c => c.Horario)
                .ToListAsync(ct);

        static string Chave(int medico, DateTime dataSlot, string horario) =>
            $"{medico}|{dataSlot:yyyy-MM-dd}|{horario}";

        var consultasPorSlot = consultas
            .GroupBy(c => Chave(c.MedicoId, c.Data.Date, c.Horario), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var slotsPorDia = new Dictionary<DateTime, List<AgendaCalendarioSlotViewModel>>();
        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
            slotsPorDia[dia.Date] = [];

        var chavesAdicionadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in disponibilidades)
        {
            if (!d.Data.HasValue || d.Medico is null)
                continue;

            var dataSlot = d.Data.Value.Date;
            var chave = Chave(d.MedicoId, dataSlot, d.Horario);
            consultasPorSlot.TryGetValue(chave, out var consulta);

            slotsPorDia[dataSlot].Add(new AgendaCalendarioSlotViewModel
            {
                DisponibilidadeId = d.Id,
                MedicoId = d.MedicoId,
                MedicoNome = d.Medico.Nome,
                Especialidade = d.Medico.EspecialidadeCadastro?.Nome ?? d.Medico.Especialidade,
                Horario = d.Horario,
                Ativo = d.Ativo,
                Encaixe = d.AgendaExcecao?.Tipo == AgendaExcecaoTipo.Encaixe,
                BloqueioManual = d.BloqueioManual,
                ConsultaId = consulta?.Id,
                PacienteNome = consulta?.Paciente?.Nome,
                StatusConsulta = consulta?.Status
            });

            chavesAdicionadas.Add(chave);
        }

        foreach (var consulta in consultas)
        {
            var chave = Chave(consulta.MedicoId, consulta.Data.Date, consulta.Horario);
            if (chavesAdicionadas.Contains(chave) || consulta.Medico is null)
                continue;

            slotsPorDia[consulta.Data.Date].Add(new AgendaCalendarioSlotViewModel
            {
                MedicoId = consulta.MedicoId,
                MedicoNome = consulta.Medico.Nome,
                Especialidade = consulta.Medico.EspecialidadeCadastro?.Nome ?? consulta.Medico.Especialidade,
                Horario = consulta.Horario,
                Ativo = false,
                ConsultaId = consulta.Id,
                PacienteNome = consulta.Paciente?.Nome,
                StatusConsulta = consulta.Status
            });
        }

        var dias = slotsPorDia
            .OrderBy(x => x.Key)
            .Select(x => new AgendaCalendarioDiaViewModel
            {
                Data = x.Key,
                Slots = x.Value
                    .OrderBy(s => s.Horario)
                    .ThenBy(s => s.MedicoNome)
                    .ToList()
            })
            .ToList();

        var model = new AgendaViewModel
        {
            DataReferencia = referencia,
            InicioPeriodo = inicio,
            FimPeriodo = fim,
            Modo = modoNormalizado,
            MedicoId = medicoId,
            EspecialidadeId = especialidadeId,
            Medicos = await _context.Medicos
                .AsNoTracking()
                .Where(m => m.Ativo)
                .OrderBy(m => m.Nome)
                .ToListAsync(ct),
            Especialidades = await _context.Especialidades
                .AsNoTracking()
                .Where(e => e.Ativo && e.Medicos.Any(m => m.Ativo))
                .OrderBy(e => e.Nome)
                .ToListAsync(ct),
            Dias = dias
        };

        ViewBag.HojeClinica = _clock.Hoje;
        return View(model);
    }
}
