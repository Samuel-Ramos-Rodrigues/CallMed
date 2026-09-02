using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Agendamento;

public sealed record DataDisponivel(DateTime Data, IReadOnlyList<string> Horarios);

public sealed record OpcaoAgendamento(
    int MedicoId,
    string Medico,
    string Especialidade,
    DateTime Data,
    string Horario);

public sealed record ResultadoOperacaoConsulta(
    bool Sucesso,
    string Mensagem,
    Consulta? Consulta = null);

public sealed class AgendamentoService
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;
    private readonly EspecialidadeService _especialidades;
    private readonly ConvenioService _convenio;
    private readonly ConvenioElegibilidadeService _elegibilidadeConvenio;
    private readonly ILogger<AgendamentoService> _logger;

    public AgendamentoService(
        MKSANContext context,
        IClinicaClock clock,
        EspecialidadeService especialidades,
        ConvenioService convenio,
        ConvenioElegibilidadeService elegibilidadeConvenio,
        ILogger<AgendamentoService> logger)
    {
        _context = context;
        _clock = clock;
        _especialidades = especialidades;
        _convenio = convenio;
        _elegibilidadeConvenio = elegibilidadeConvenio;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DateTime>> DatasDisponiveisAsync(
        int medicoId,
        int? ignorarConsultaId = null,
        CancellationToken ct = default)
    {
        if (medicoId <= 0)
            return [];

        var hoje = _clock.Hoje;
        var agora = _clock.Agora;

        var disponibilidades = await _context.Disponibilidades
            .AsNoTracking()
            .Where(d =>
                d.MedicoId == medicoId &&
                d.Ativo &&
                d.Data.HasValue &&
                d.Data.Value.Date >= hoje)
            .Select(d => new { Data = d.Data!.Value, d.Horario })
            .ToListAsync(ct);

        if (disponibilidades.Count == 0)
            return [];

        var primeira = disponibilidades.Min(x => x.Data.Date);
        var ultima = disponibilidades.Max(x => x.Data.Date);

        var ocupados = await _context.Consultas
            .AsNoTracking()
            .Where(c =>
                c.MedicoId == medicoId &&
                c.Data.Date >= primeira &&
                c.Data.Date <= ultima &&
                c.Status != ConsultaStatus.Cancelada &&
                (!ignorarConsultaId.HasValue || c.Id != ignorarConsultaId.Value))
            .Select(c => new { c.Data, c.Horario })
            .ToListAsync(ct);

        var ocupadosSet = ocupados
            .Select(c => ChaveSlot(c.Data, c.Horario))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return disponibilidades
            .Where(d => HorarioEhFuturo(d.Data, d.Horario, agora))
            .Where(d => !ocupadosSet.Contains(ChaveSlot(d.Data, d.Horario)))
            .Select(d => d.Data.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> HorariosDisponiveisAsync(
        int medicoId,
        DateTime data,
        int? ignorarConsultaId = null,
        CancellationToken ct = default)
    {
        if (medicoId <= 0 || data == default || data.Date < _clock.Hoje)
            return [];

        var dataConsulta = data.Date;
        var agora = _clock.Agora;

        var horarios = await _context.Disponibilidades
            .AsNoTracking()
            .Where(d =>
                d.MedicoId == medicoId &&
                d.Ativo &&
                d.Data.HasValue &&
                d.Data.Value.Date == dataConsulta)
            .Select(d => d.Horario)
            .Distinct()
            .ToListAsync(ct);

        var ocupados = await _context.Consultas
            .AsNoTracking()
            .Where(c =>
                c.MedicoId == medicoId &&
                c.Data.Date == dataConsulta &&
                c.Status != ConsultaStatus.Cancelada &&
                (!ignorarConsultaId.HasValue || c.Id != ignorarConsultaId.Value))
            .Select(c => c.Horario)
            .ToListAsync(ct);

        var setOcupados = ocupados.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return horarios
            .Where(h => HorarioEhFuturo(dataConsulta, h, agora))
            .Where(h => !setOcupados.Contains(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => HoraOrdenacao(h))
            .ThenBy(h => h)
            .ToList();
    }

    public async Task<bool> HorarioDisponivelAsync(
        int medicoId,
        DateTime data,
        string? horario,
        int? ignorarConsultaId = null,
        CancellationToken ct = default)
    {
        if (medicoId <= 0 ||
            data == default ||
            string.IsNullOrWhiteSpace(horario) ||
            !HorarioEhFuturo(data, horario, _clock.Agora))
            return false;

        var dataConsulta = data.Date;
        var hora = horario.Trim();

        var existeDisponibilidade = await _context.Disponibilidades
            .AsNoTracking()
            .AnyAsync(d =>
                d.MedicoId == medicoId &&
                d.Ativo &&
                d.Data.HasValue &&
                d.Data.Value.Date == dataConsulta &&
                d.Horario == hora,
                ct);

        if (!existeDisponibilidade)
            return false;

        return !await _context.Consultas
            .AsNoTracking()
            .AnyAsync(c =>
                c.MedicoId == medicoId &&
                c.Data.Date == dataConsulta &&
                c.Horario == hora &&
                c.Status != ConsultaStatus.Cancelada &&
                (!ignorarConsultaId.HasValue || c.Id != ignorarConsultaId.Value),
                ct);
    }

    public async Task<IReadOnlyList<OpcaoAgendamento>> BuscarOpcoesAsync(
        string? nomeMedico,
        string? especialidade,
        DateTime? dataInicio = null,
        int quantidadeDias = 90,
        int limite = 3,
        CancellationToken ct = default)
    {
        var medicos = await _especialidades.BuscarMedicosAsync(
            especialidade,
            nomeMedico,
            ct);

        if (medicos.Count == 0)
            return [];

        var inicio = dataInicio?.Date > _clock.Hoje
            ? dataInicio.Value.Date
            : _clock.Hoje;

        quantidadeDias = Math.Clamp(quantidadeDias, 1, 90);
        limite = Math.Clamp(limite, 1, 20);
        var fim = inicio.AddDays(quantidadeDias - 1);
        var ids = medicos.Select(m => m.Id).ToArray();
        var mapa = medicos.ToDictionary(m => m.Id);
        var agora = _clock.Agora;

        var disponibilidades = await _context.Disponibilidades
            .AsNoTracking()
            .Where(d =>
                ids.Contains(d.MedicoId) &&
                d.Ativo &&
                d.Data.HasValue &&
                d.Data.Value.Date >= inicio &&
                d.Data.Value.Date <= fim)
            .Select(d => new { d.MedicoId, Data = d.Data!.Value, d.Horario })
            .ToListAsync(ct);

        var consultas = await _context.Consultas
            .AsNoTracking()
            .Where(c =>
                ids.Contains(c.MedicoId) &&
                c.Data.Date >= inicio &&
                c.Data.Date <= fim &&
                c.Status != ConsultaStatus.Cancelada)
            .Select(c => new { c.MedicoId, c.Data, c.Horario })
            .ToListAsync(ct);

        var ocupados = consultas
            .Select(c => $"{c.MedicoId}|{ChaveSlot(c.Data, c.Horario)}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return disponibilidades
            .Where(d => HorarioEhFuturo(d.Data, d.Horario, agora))
            .Where(d => !ocupados.Contains($"{d.MedicoId}|{ChaveSlot(d.Data, d.Horario)}"))
            .GroupBy(d => new { d.MedicoId, Data = d.Data.Date, Horario = d.Horario })
            .Select(g => g.First())
            .OrderBy(d => d.Data.Date)
            .ThenBy(d => HoraOrdenacao(d.Horario))
            .ThenBy(d => mapa[d.MedicoId].Nome)
            .Take(limite)
            .Select(d => new OpcaoAgendamento(
                d.MedicoId,
                mapa[d.MedicoId].Nome,
                _especialidades.CanonicalizarNome(mapa[d.MedicoId].Especialidade),
                d.Data.Date,
                d.Horario))
            .ToList();
    }

    public async Task<ResultadoOperacaoConsulta> AgendarAsync(
        int pacienteId,
        int medicoId,
        DateTime data,
        string horario,
        string? observacao,
        string? tipoPagamento = null,
        bool permitirEscolhaPagamento = false,
        CancellationToken ct = default,
        bool permitirConvenioSemMatriz = false)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null || !paciente.Ativo)
            return Falha("Paciente inválido ou inativo.");

        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.Id == medicoId && m.Ativo, ct);
        if (medico is null)
            return Falha("Médico inválido ou inativo.");

        if (data == default || data.Date < _clock.Hoje)
            return Falha("Selecione uma data válida.");

        if (!HorarioEhFuturo(data, horario, _clock.Agora))
            return Falha("Esse horário já passou.");

        if (permitirEscolhaPagamento &&
            TipoPagamentoConsulta.Normalizar(tipoPagamento) == TipoPagamentoConsulta.Convenio &&
            !_convenio.EhValido(paciente))
        {
            return Falha("O paciente não possui convênio válido para esse atendimento.");
        }

        var usarConvenio = permitirEscolhaPagamento
            ? string.IsNullOrWhiteSpace(tipoPagamento)
                ? _convenio.EhValido(paciente)
                : TipoPagamentoConsulta.Normalizar(tipoPagamento) == TipoPagamentoConsulta.Convenio
            : _convenio.EhValido(paciente);

        if (usarConvenio && medico.EspecialidadeId.HasValue)
        {
            var elegibilidade = await _elegibilidadeConvenio.AvaliarAsync(paciente, medico.EspecialidadeId, ct);
            if (!elegibilidade.RegrasConfiguradas && !permitirConvenioSemMatriz)
                return Falha("A cobertura desse convênio ainda não foi cadastrada para triagem. A equipe CallMed precisa validar antes de concluir o agendamento.");
            if (!elegibilidade.Elegivel)
                return Falha(elegibilidade.Mensagem + " Se desejar atendimento particular, procure a equipe CallMed.");
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await BloquearSlotAsync(medicoId, data, horario, ct);

        if (!await HorarioDisponivelAsync(medicoId, data, horario, null, ct))
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse horário não está mais disponível. Escolha outra vaga.");
        }

        var duplicadaPaciente = await _context.Consultas.AnyAsync(c =>
            c.PacienteId == pacienteId &&
            c.MedicoId == medicoId &&
            c.Data.Date == data.Date &&
            c.Horario == horario.Trim() &&
            c.Status != ConsultaStatus.Cancelada,
            ct);

        if (duplicadaPaciente)
        {
            await tx.RollbackAsync(ct);
            return Falha("O paciente já possui essa consulta agendada.");
        }

        var consulta = new Consulta
        {
            PacienteId = pacienteId,
            MedicoId = medicoId,
            Data = data.Date,
            Horario = horario.Trim(),
            Status = ConsultaStatus.Pendente,
            Observacao = Limitar(observacao, 1000),
            CriadoEm = DateTime.UtcNow
        };

        _convenio.AplicarPagamento(
            consulta,
            paciente,
            tipoPagamento,
            permitirEscolhaPagamento);

        _context.Consultas.Add(consulta);

        var pedidosEspera = await _context.ListasEspera
            .Where(x => x.Ativa && x.PacienteId == pacienteId &&
                (x.MedicoId == medicoId || (!x.MedicoId.HasValue && x.EspecialidadeId == medico.EspecialidadeId)))
            .ToListAsync(ct);
        foreach (var pedido in pedidosEspera.Where(x =>
                     (!x.DataPreferida.HasValue || x.DataPreferida.Value.Date == data.Date) &&
                     PeriodoListaEsperaAceito(horario, x.Periodo)))
        {
            pedido.Ativa = false;
            pedido.AtualizadoEm = DateTime.UtcNow;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, "Consulta agendada com sucesso.", consulta);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Conflito ao agendar slot {MedicoId}/{Data}/{Horario}.", medicoId, data.Date, horario);
            return Falha("Esse horário acabou de ser ocupado. Escolha outra vaga.");
        }
    }

    public async Task<ResultadoOperacaoConsulta> EditarAsync(
        int consultaId,
        int pacienteId,
        int medicoId,
        DateTime data,
        string horario,
        string? observacao,
        string? tipoPagamento,
        CancellationToken ct = default)
    {
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");

        if (!ConsultaStatus.PodeRemarcar(consulta.Status))
            return Falha("Consultas canceladas ou realizadas não podem ser editadas.");

        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null)
            return Falha("Paciente inválido ou inativo.");

        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.Id == medicoId && m.Ativo, ct);
        if (medico is null)
            return Falha("Médico inválido ou inativo.");

        if (TipoPagamentoConsulta.Normalizar(tipoPagamento) == TipoPagamentoConsulta.Convenio &&
            !_convenio.EhValido(paciente))
            return Falha("O paciente não possui convênio válido.");

        var usarConvenioEdicao = string.IsNullOrWhiteSpace(tipoPagamento)
            ? _convenio.EhValido(paciente)
            : TipoPagamentoConsulta.Normalizar(tipoPagamento) == TipoPagamentoConsulta.Convenio;
        if (usarConvenioEdicao && medico.EspecialidadeId.HasValue)
        {
            var elegibilidade = await _elegibilidadeConvenio.AvaliarAsync(paciente, medico.EspecialidadeId, ct);
            if (!elegibilidade.RegrasConfiguradas) return Falha("A matriz de cobertura do convênio precisa ser cadastrada antes de usar convênio nesta consulta.");
            if (!elegibilidade.Elegivel) return Falha(elegibilidade.Mensagem);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await BloquearSlotAsync(medicoId, data, horario, ct);

        if (!await HorarioDisponivelAsync(medicoId, data, horario, consultaId, ct))
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse horário já está ocupado ou indisponível.");
        }

        consulta.PacienteId = pacienteId;
        consulta.MedicoId = medicoId;
        var mudouHorario = consulta.Data.Date != data.Date || !string.Equals(consulta.Horario, horario.Trim(), StringComparison.OrdinalIgnoreCase);
        consulta.Data = data.Date;
        consulta.Horario = horario.Trim();
        if (mudouHorario)
        {
            consulta.Lembrete24hEnviadoEm = null;
            consulta.Lembrete2hEnviadoEm = null;
        }
        consulta.Observacao = Limitar(observacao, 1000);
        _convenio.AplicarPagamento(consulta, paciente, tipoPagamento, permitirEscolha: true);

        try
        {
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, "Consulta atualizada com sucesso.", consulta);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse horário acabou de ser ocupado. Escolha outra vaga.");
        }
    }

    public async Task<ResultadoOperacaoConsulta> RemarcarAsync(
        int consultaId,
        DateTime data,
        string horario,
        CancellationToken ct = default)
    {
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");

        if (!ConsultaStatus.PodeRemarcar(consulta.Status))
            return Falha("Essa consulta não pode mais ser remarcada.");

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await BloquearSlotAsync(consulta.MedicoId, data, horario, ct);

        if (!await HorarioDisponivelAsync(consulta.MedicoId, data, horario, consulta.Id, ct))
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse novo horário já está ocupado ou indisponível.");
        }

        consulta.Data = data.Date;
        consulta.Horario = horario.Trim();
        consulta.Lembrete24hEnviadoEm = null;
        consulta.Lembrete2hEnviadoEm = null;
        consulta.Status = ConsultaStatus.Remarcada;
        consulta.ConfirmadaEm = null;

        try
        {
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, "Consulta remarcada com sucesso.", consulta);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse horário acabou de ser ocupado. Escolha outra vaga.");
        }
    }

    public async Task<ResultadoOperacaoConsulta> ConfirmarAsync(
        int consultaId,
        CancellationToken ct = default)
    {
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");

        if (string.Equals(consulta.Status, ConsultaStatus.Confirmada, StringComparison.OrdinalIgnoreCase))
            return new(true, "A consulta já está confirmada.", consulta);

        if (!ConsultaStatus.PodeConfirmar(consulta.Status))
            return Falha("Essa consulta não pode ser confirmada nesse estado.");

        consulta.Status = ConsultaStatus.Confirmada;
        consulta.ConfirmadaEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return new(true, "Consulta confirmada com sucesso.", consulta);
    }

    public async Task<ResultadoOperacaoConsulta> CancelarAsync(
        int consultaId,
        CancellationToken ct = default)
    {
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");

        if (string.Equals(consulta.Status, ConsultaStatus.Cancelada, StringComparison.OrdinalIgnoreCase))
            return new(true, "A consulta já está cancelada.", consulta);

        if (!ConsultaStatus.PodeCancelar(consulta.Status))
            return Falha("Essa consulta não pode ser cancelada nesse estado.");

        consulta.Status = ConsultaStatus.Cancelada;
        consulta.CanceladaEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return new(true, "Consulta cancelada com sucesso. A vaga foi liberada para a lista de espera.", consulta);
    }

    public async Task<ResultadoOperacaoConsulta> RealizarAsync(
        int consultaId,
        CancellationToken ct = default)
    {
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");

        if (!ConsultaStatus.PodeRealizar(consulta.Status))
            return Falha("A consulta precisa estar confirmada ou remarcada para ser concluída.");

        if (consulta.Data.Date > _clock.Hoje ||
            (consulta.Data.Date == _clock.Hoje &&
             HorarioEhFuturo(consulta.Data, consulta.Horario, _clock.Agora)))
        {
            return Falha("Uma consulta que ainda não aconteceu não pode ser marcada como realizada.");
        }

        consulta.Status = ConsultaStatus.Realizada;
        consulta.RealizadaEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return new(true, "Consulta marcada como realizada.", consulta);
    }

    public async Task<ResultadoOperacaoConsulta> MarcarAusenteAsync(
        int consultaId,
        CancellationToken ct = default)
    {
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null) return Falha("Consulta não encontrada.");
        if (!ConsultaStatus.PodeMarcarAusente(consulta.Status))
            return Falha("Essa consulta não pode ser marcada como ausência.");

        if (consulta.Data.Date > _clock.Hoje ||
            (consulta.Data.Date == _clock.Hoje && HorarioEhFuturo(consulta.Data, consulta.Horario, _clock.Agora)))
            return Falha("Não é possível registrar ausência antes do horário da consulta.");

        consulta.Status = ConsultaStatus.Ausente;
        consulta.AusenteEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return new(true, "Ausência registrada para fins de acompanhamento do absenteísmo.", consulta);
    }

    public Task<bool> ExisteConsultaAtivaNoSlotAsync(
        int medicoId,
        DateTime data,
        string horario,
        int? ignorarConsultaId = null,
        CancellationToken ct = default)
    {
        var dia = data.Date;
        var hora = horario.Trim();

        return _context.Consultas.AnyAsync(c =>
            c.MedicoId == medicoId &&
            c.Data.Date == dia &&
            c.Horario == hora &&
            c.Status != ConsultaStatus.Cancelada &&
            (!ignorarConsultaId.HasValue || c.Id != ignorarConsultaId.Value),
            ct);
    }

    private async Task BloquearSlotAsync(
        int medicoId,
        DateTime data,
        string horario,
        CancellationToken ct)
    {
        var chave = $"mksan:{medicoId}:{data:yyyy-MM-dd}:{horario.Trim()}";

        // Neon/PostgreSQL: serializa tentativas concorrentes do mesmo slot.
        // O índice único parcial no banco continua sendo a segunda camada de proteção.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({chave}, 0));",
            ct);
    }

    private static bool HorarioEhFuturo(DateTime data, string? horario, DateTime agora)
    {
        if (data.Date < agora.Date || string.IsNullOrWhiteSpace(horario))
            return false;

        if (data.Date > agora.Date)
            return true;

        if (!TimeOnly.TryParseExact(
                horario.Trim(),
                new[] { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var hora))
            return false;

        return hora > TimeOnly.FromDateTime(agora);
    }

    private static TimeOnly HoraOrdenacao(string? horario)
    {
        return TimeOnly.TryParse(horario, out var hora)
            ? hora
            : TimeOnly.MaxValue;
    }

    private static string ChaveSlot(DateTime data, string horario) =>
        $"{data.Date:yyyy-MM-dd}|{horario.Trim()}";

    private static string? Limitar(string? texto, int limite)
    {
        var valor = texto?.Trim();
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        return valor.Length <= limite ? valor : valor[..limite];
    }

    private static bool PeriodoListaEsperaAceito(string horario, string? periodo)
    {
        var p = periodo?.Trim();
        if (string.IsNullOrWhiteSpace(p) || p.Equals("Qualquer", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!TimeSpan.TryParse(horario, out var hora))
            return true;
        return p.ToLowerInvariant() switch
        {
            "manhã" or "manha" => hora < TimeSpan.FromHours(12),
            "tarde" => hora >= TimeSpan.FromHours(12) && hora < TimeSpan.FromHours(18),
            "noite" => hora >= TimeSpan.FromHours(18),
            _ => true
        };
    }

    private static ResultadoOperacaoConsulta Falha(string mensagem) =>
        new(false, mensagem);
}
