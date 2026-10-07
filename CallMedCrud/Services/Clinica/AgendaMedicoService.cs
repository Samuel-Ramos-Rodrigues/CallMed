using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Services.Clinica;

public sealed class AgendaMedicoService
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;

    public AgendaMedicoService(MKSANContext context, IClinicaClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public static IReadOnlyList<string> HorariosPadrao { get; } =
        Enumerable.Range(14, 27)
            .Select(x => TimeSpan.FromMinutes(x * 30))
            .Where(x => x <= new TimeSpan(20, 0, 0))
            .Select(x => x.ToString(@"hh\:mm"))
            .ToList();

    public async Task<List<AgendaDiaViewModel>> ObterAgendaAsync(int medicoId, CancellationToken ct = default)
    {
        var agenda = AgendaDiaViewModel.CriarSemana();
        var itens = await _context.Set<MedicoHorarioSemanal>()
            .AsNoTracking()
            .Where(x => x.MedicoId == medicoId && x.Ativo)
            .OrderBy(x => x.DiaSemana)
            .ThenBy(x => x.Horario)
            .ToListAsync(ct);

        foreach (var dia in agenda)
        {
            dia.Horarios = itens
                .Where(x => x.DiaSemana == dia.DiaSemana)
                .Select(x => x.Horario)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
            dia.Trabalha = dia.Horarios.Count > 0;
        }

        return agenda;
    }

    /// <summary>
    /// Retorna consultas futuras que deixariam de pertencer à nova agenda semanal.
    /// A edição deve ser bloqueada até que essas consultas sejam remarcadas/canceladas.
    /// </summary>
    public async Task<List<Consulta>> ObterConsultasFuturasForaDaAgendaAsync(
        int medicoId,
        IEnumerable<AgendaDiaViewModel>? agenda,
        CancellationToken ct = default)
    {
        var permitidos = (agenda ?? [])
            .Where(d => d.Trabalha)
            .SelectMany(d => (d.Horarios ?? [])
                .Where(HorarioValido)
                .Select(h => ChaveSemanal(d.DiaSemana, h)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var consultas = await _context.Consultas
            .AsNoTracking()
            .Where(c =>
                c.MedicoId == medicoId &&
                c.Data.Date >= _clock.Hoje &&
                c.Status != ConsultaStatus.Cancelada)
            .OrderBy(c => c.Data)
            .ThenBy(c => c.Horario)
            .ToListAsync(ct);

        return consultas
            .Where(c => !permitidos.Contains(ChaveSemanal((int)c.Data.DayOfWeek, c.Horario)))
            .ToList();
    }

    public async Task SalvarAgendaAsync(
        int medicoId,
        IEnumerable<AgendaDiaViewModel>? agenda,
        CancellationToken ct = default)
    {
        var existentes = await _context.Set<MedicoHorarioSemanal>()
            .Where(x => x.MedicoId == medicoId)
            .ToListAsync(ct);

        _context.RemoveRange(existentes);

        var novos = (agenda ?? [])
            .Where(d => d.Trabalha)
            .SelectMany(d => (d.Horarios ?? [])
                .Where(HorarioValido)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(h => new MedicoHorarioSemanal
                {
                    MedicoId = medicoId,
                    DiaSemana = d.DiaSemana,
                    Horario = NormalizarHorario(h),
                    Ativo = true
                }))
            .ToList();

        if (novos.Count > 0)
            _context.AddRange(novos);

        await _context.SaveChangesAsync(ct);
        await SincronizarDisponibilidadesFuturasAsync(medicoId, 120, ct);
    }

    /// <summary>
    /// Mantém uma janela rolante de disponibilidades futuras geradas pela agenda semanal.
    /// </summary>
    public async Task SincronizarDisponibilidadesFuturasAsync(
        int medicoId,
        int dias = 120,
        CancellationToken ct = default)
    {
        var inicio = _clock.Hoje;
        var fim = inicio.AddDays(Math.Clamp(dias, 30, 365));

        var medicoAtivo = await _context.Medicos
            .AsNoTracking()
            .Where(m => m.Id == medicoId)
            .Select(m => m.Ativo)
            .FirstOrDefaultAsync(ct);

        var regras = medicoAtivo
            ? await _context.Set<MedicoHorarioSemanal>()
                .AsNoTracking()
                .Where(x => x.MedicoId == medicoId && x.Ativo)
                .ToListAsync(ct)
            : new List<MedicoHorarioSemanal>();

        var desejados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var data = inicio; data <= fim; data = data.AddDays(1))
        {
            var horarios = regras.Where(r => r.DiaSemana == (int)data.DayOfWeek);
            foreach (var regra in horarios)
                desejados.Add(Chave(data, regra.Horario));
        }

        var disponibilidadesExistentes = await _context.Disponibilidades
            .Where(d => d.MedicoId == medicoId &&
                        d.Data.HasValue &&
                        d.Data.Value.Date >= inicio &&
                        d.Data.Value.Date <= fim)
            .ToListAsync(ct);

        foreach (var item in disponibilidadesExistentes.Where(d => d.OrigemAgendaSemanal))
        {
            item.Ativo = !item.BloqueioManual &&
                         item.Data.HasValue &&
                         desejados.Contains(Chave(item.Data.Value.Date, item.Horario));
        }

        var existentes = disponibilidadesExistentes
            .Where(d => d.Data.HasValue)
            .Select(d => Chave(d.Data!.Value.Date, d.Horario))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var chave in desejados.Where(k => !existentes.Contains(k)))
        {
            var partes = chave.Split('|');
            _context.Disponibilidades.Add(new Disponibilidade
            {
                MedicoId = medicoId,
                Data = DateTime.ParseExact(partes[0], "yyyy-MM-dd", null),
                Horario = partes[1],
                Ativo = true,
                OrigemAgendaSemanal = true,
                BloqueioManual = false
            });
        }

        await _context.SaveChangesAsync(ct);
        await AplicarExcecoesAtivasAsync(medicoId, inicio, fim, ct);
    }

    private async Task AplicarExcecoesAtivasAsync(
        int medicoId,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct)
    {
        var excecoes = await _context.AgendaExcecoes
            .AsNoTracking()
            .Where(x =>
                x.MedicoId == medicoId &&
                x.Ativa &&
                x.Data.Date >= inicio &&
                x.Data.Date <= fim)
            .OrderBy(x => x.Data)
            .ThenBy(x => x.HorarioInicio)
            .ToListAsync(ct);

        if (excecoes.Count == 0)
            return;

        var slots = await _context.Disponibilidades
            .Where(d =>
                d.MedicoId == medicoId &&
                d.Data.HasValue &&
                d.Data.Value.Date >= inicio &&
                d.Data.Value.Date <= fim)
            .ToListAsync(ct);

        foreach (var excecao in excecoes)
        {
            if (excecao.Tipo == AgendaExcecaoTipo.Encaixe)
            {
                if (!HorarioValido(excecao.HorarioInicio))
                    continue;

                var horario = NormalizarHorario(excecao.HorarioInicio!);
                var slot = slots.FirstOrDefault(d =>
                    d.Data.HasValue &&
                    d.Data.Value.Date == excecao.Data.Date &&
                    string.Equals(NormalizarHorario(d.Horario), horario, StringComparison.OrdinalIgnoreCase));

                if (slot is null)
                {
                    slot = new Disponibilidade
                    {
                        MedicoId = medicoId,
                        Data = excecao.Data.Date,
                        Horario = horario,
                        Ativo = true,
                        OrigemAgendaSemanal = false,
                        BloqueioManual = false,
                        AgendaExcecaoId = excecao.Id
                    };

                    _context.Disponibilidades.Add(slot);
                    slots.Add(slot);
                }
                else if (!slot.BloqueioManual)
                {
                    slot.Ativo = true;
                    slot.AgendaExcecaoId = excecao.Id;
                }

                continue;
            }

            IEnumerable<Disponibilidade> afetados = slots.Where(d =>
                d.Data.HasValue && d.Data.Value.Date == excecao.Data.Date);

            if (excecao.Tipo == AgendaExcecaoTipo.Bloqueio && HorarioValido(excecao.HorarioInicio))
            {
                var inicioBloqueio = TimeSpan.Parse(NormalizarHorario(excecao.HorarioInicio!));
                var fimBloqueio = HorarioValido(excecao.HorarioFim)
                    ? TimeSpan.Parse(NormalizarHorario(excecao.HorarioFim!))
                    : inicioBloqueio;

                if (fimBloqueio < inicioBloqueio)
                    (inicioBloqueio, fimBloqueio) = (fimBloqueio, inicioBloqueio);

                afetados = afetados.Where(d =>
                    TimeSpan.TryParse(d.Horario, out var hora) &&
                    hora >= inicioBloqueio &&
                    hora <= fimBloqueio);
            }

            foreach (var slot in afetados)
            {
                // Um bloqueio criado diretamente pelo médico não deve ser
                // tomado por uma exceção administrativa sobreposta.
                if (slot.BloqueioManual && !slot.AgendaExcecaoId.HasValue)
                    continue;

                slot.Ativo = false;
                slot.BloqueioManual = true;
                slot.AgendaExcecaoId = excecao.Id;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task SincronizarTodosMedicosAtivosAsync(
        int dias = 120,
        CancellationToken ct = default)
    {
        var ids = await _context.Medicos
            .AsNoTracking()
            .Where(m => m.Ativo)
            .Select(m => m.Id)
            .ToListAsync(ct);

        foreach (var id in ids)
            await SincronizarDisponibilidadesFuturasAsync(id, dias, ct);
    }

    private static bool HorarioValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) &&
        TimeSpan.TryParse(valor, out var hora) &&
        hora >= TimeSpan.Zero &&
        hora < TimeSpan.FromDays(1);

    private static string NormalizarHorario(string horario) =>
        TimeSpan.TryParse(horario, out var hora)
            ? hora.ToString(@"hh\:mm")
            : horario.Trim();

    private static string Chave(DateTime data, string horario) =>
        $"{data:yyyy-MM-dd}|{NormalizarHorario(horario)}";

    private static string ChaveSemanal(int diaSemana, string horario) =>
        $"{diaSemana}|{NormalizarHorario(horario)}";
}
