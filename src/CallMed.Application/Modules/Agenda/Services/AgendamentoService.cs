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
    private readonly EncaixeService? _encaixes;
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
        ILogger<AgendamentoService> logger, EncaixeService? encaixes = null)
    {
        _context = context; _encaixes = encaixes;
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

        var holds=await _context.Ofertas.AsNoTracking().Where(o=>o.MedicoId==medicoId&&o.Estado=="Aberta"&&o.ExpiraEm>DateTime.UtcNow).Select(o=>new{o.Data,o.Horario}).ToListAsync(ct);
        ocupados.AddRange(holds);
        var ocupadosSet = ocupados
            .Select(c => ChaveSlot(c.Data, c.Horario))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidatos = disponibilidades
            .Where(d => HorarioEhFuturo(d.Data, d.Horario, agora))
            .Where(d => !ocupadosSet.Contains(ChaveSlot(d.Data, d.Horario)))
            .ToList();
        var mapa=await CarregarOcupacaoAsync(medicoId,primeira,ultima,ignorarConsultaId,null,ct);
        var diasLivres=new HashSet<DateTime>();
        foreach(var d in candidatos)if(!diasLivres.Contains(d.Data.Date)&&mapa.Livre(d.Data,d.Horario))diasLivres.Add(d.Data.Date);
        return diasLivres.OrderBy(d=>d).ToList();
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

        var holds=await _context.Ofertas.AsNoTracking().Where(o=>o.MedicoId==medicoId&&o.Data==dataConsulta&&o.Estado=="Aberta"&&o.ExpiraEm>DateTime.UtcNow).Select(o=>o.Horario).ToListAsync(ct);
        ocupados.AddRange(holds);
        var setOcupados = ocupados.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidatos = horarios
            .Where(h => HorarioEhFuturo(dataConsulta, h, agora))
            .Where(h => !setOcupados.Contains(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => HoraOrdenacao(h))
            .ThenBy(h => h)
            .ToList();
        var mapa=await CarregarOcupacaoAsync(medicoId,dataConsulta,dataConsulta,ignorarConsultaId,null,ct);var livres=new List<string>();foreach(var h in candidatos)if(mapa.Livre(dataConsulta,h))livres.Add(h);return livres;
    }

    public async Task<bool> HorarioDisponivelAsync(
        int medicoId,
        DateTime data,
        string? horario,
        int? ignorarConsultaId = null,
        CancellationToken ct = default, Guid? ofertaId = null)
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
        if(!await IntervaloLivreAsync(medicoId,dataConsulta,hora,ignorarConsultaId,ofertaId,ct))return false;

        if(await _context.Ofertas.AnyAsync(o=>o.MedicoId==medicoId&&o.Data==dataConsulta&&o.Horario==hora&&o.Estado=="Aberta"&&o.ExpiraEm>DateTime.UtcNow&&(!ofertaId.HasValue||o.Id!=ofertaId.Value),ct))return false;
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

        var holds=await _context.Ofertas.AsNoTracking().Where(o=>ids.Contains(o.MedicoId)&&o.Estado=="Aberta"&&o.ExpiraEm>DateTime.UtcNow).Select(o=>new{o.MedicoId,o.Data,o.Horario}).ToListAsync(ct);
        consultas.AddRange(holds);
        var ocupados = consultas
            .Select(c => $"{c.MedicoId}|{ChaveSlot(c.Data, c.Horario)}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidatos = disponibilidades
            .Where(d => HorarioEhFuturo(d.Data, d.Horario, agora))
            .Where(d => !ocupados.Contains($"{d.MedicoId}|{ChaveSlot(d.Data, d.Horario)}"))
            .GroupBy(d => new { d.MedicoId, Data = d.Data.Date, Horario = d.Horario })
            .Select(g => g.First())
            .OrderBy(d => d.Data.Date)
            .ThenBy(d => HoraOrdenacao(d.Horario))
            .ThenBy(d => mapa[d.MedicoId].Nome)
            .Select(d => new OpcaoAgendamento(
                d.MedicoId,
                mapa[d.MedicoId].Nome,
                _especialidades.CanonicalizarNome(mapa[d.MedicoId].Especialidade),
                d.Data.Date,
                d.Horario))
            .ToList();
        var mapas=new Dictionary<int,Ocupacao>();var opcoes=new List<OpcaoAgendamento>();foreach(var d in candidatos){if(!mapas.TryGetValue(d.MedicoId,out var ocupacao)){ocupacao=await CarregarOcupacaoAsync(d.MedicoId,inicio,fim,null,null,ct);mapas.Add(d.MedicoId,ocupacao);}if(ocupacao.Livre(d.Data,d.Horario))opcoes.Add(d);if(opcoes.Count>=limite)break;}return opcoes;
    }

    public async Task<ResultadoOperacaoConsulta> AgendarAsync(
        int pacienteId,
        int medicoId,
        DateTime data,
        string horario,
        string? observacao,
        string? tipoPagamento = null,
        bool permitirEscolhaPagamento = false,
        CancellationToken ct = default, Guid? ofertaId = null)
    {
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId, ct);
        if (paciente is null || !paciente.Ativo)
            return Falha("Paciente inválido ou inativo.");

        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.Id == medicoId && m.Ativo, ct);
        if (medico is null)
            return Falha("Psicólogo inválido ou inativo.");

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
            // A ausência total de matriz não impede a reserva de um horário:
            // o cadastro do convênio é aproveitado e a consulta é confirmada.
            // Quando houver uma regra explícita de não cobertura, o usuário
            // deverá optar por atendimento particular ou outro profissional.
            if (elegibilidade.RegrasConfiguradas && !elegibilidade.Elegivel)
                return Falha(elegibilidade.Mensagem + " Selecione atendimento particular ou escolha outra especialidade.");
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await BloquearSlotAsync(medicoId, data, horario, ct);

        Oferta? oferta=null;
        if(ofertaId.HasValue){oferta=await _context.Ofertas.Include(o=>o.ListaEspera).SingleOrDefaultAsync(o=>o.Id==ofertaId,ct);if(oferta==null||oferta.Estado!="Aberta"||oferta.ExpiraEm<=DateTime.UtcNow||!oferta.ListaEspera.Ativa||oferta.ListaEspera.PacienteId!=pacienteId||oferta.MedicoId!=medicoId||oferta.Data.Date!=data.Date||oferta.Horario!=horario.Trim())return Falha("Oferta expirada ou indisponível.");}
        if (!await HorarioDisponivelAsync(medicoId, data, horario, null, ct, ofertaId))
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
            DuracaoMinutos = medico.DuracaoPadraoMinutos,
            MedicoId = medicoId,
            Data = data.Date,
            Horario = horario.Trim(),
            Status = ConsultaStatus.Confirmada,
            ConfirmadaEm = DateTime.UtcNow,
            Observacao = Limitar(observacao, 1000),
            CriadoEm = DateTime.UtcNow
        };

        _convenio.AplicarPagamento(
            consulta,
            paciente,
            tipoPagamento,
            permitirEscolhaPagamento);
        await AtualizarElegibilidadeConvenioAsync(consulta, paciente, medico, ct);

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
            if(oferta!=null){oferta.Estado="Aceita";oferta.ConsultaId=consulta.Id;await _context.SaveChangesAsync(ct);}
            await tx.CommitAsync(ct);
            return new(true, MensagemAgendamento(consulta), consulta);
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
        await using var tx=await _context.Database.BeginTransactionAsync(ct);
        await LockJornadaAsync(consultaId,ct);
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");

        if(await EmAtendimentoAsync(consultaId,ct))return Falha("O atendimento já foi iniciado. Procure a equipe.");
        if (!ConsultaStatus.PodeRemarcar(consulta.Status))
            return Falha("Consultas canceladas ou realizadas não podem ser editadas.");

        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null)
            return Falha("Paciente inválido ou inativo.");

        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.Id == medicoId && m.Ativo, ct);
        if (medico is null)
            return Falha("Psicólogo inválido ou inativo.");

        if (TipoPagamentoConsulta.Normalizar(tipoPagamento) == TipoPagamentoConsulta.Convenio &&
            !_convenio.EhValido(paciente))
            return Falha("O paciente não possui convênio válido.");

        var usarConvenioEdicao = string.IsNullOrWhiteSpace(tipoPagamento)
            ? _convenio.EhValido(paciente)
            : TipoPagamentoConsulta.Normalizar(tipoPagamento) == TipoPagamentoConsulta.Convenio;
        if (usarConvenioEdicao && medico.EspecialidadeId.HasValue)
        {
            var elegibilidade = await _elegibilidadeConvenio.AvaliarAsync(paciente, medico.EspecialidadeId, ct);
            if (elegibilidade.RegrasConfiguradas && !elegibilidade.Elegivel) return Falha(elegibilidade.Mensagem);
        }

        await BloquearSlotAsync(medicoId, data, horario, ct);

        if (!await HorarioDisponivelAsync(medicoId, data, horario, consultaId, ct))
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse horário já está ocupado ou indisponível.");
        }

        var mudouVinculo=consulta.PacienteId!=pacienteId||consulta.MedicoId!=medicoId;
        consulta.PacienteId = pacienteId;
        consulta.MedicoId = medicoId;
        var mudouHorario = consulta.Data.Date != data.Date || !string.Equals(consulta.Horario, horario.Trim(), StringComparison.OrdinalIgnoreCase);
        consulta.Data = data.Date;
        consulta.Horario = horario.Trim();
        if (mudouHorario || mudouVinculo)
        {
            consulta.Revisao++;
            await ReiniciarAsync(consulta.Id,ct);
            consulta.Lembrete24hEnviadoEm = null;
            consulta.Lembrete2hEnviadoEm = null;
        }
        consulta.Observacao = Limitar(observacao, 1000);
        _convenio.AplicarPagamento(consulta, paciente, tipoPagamento, permitirEscolha: true);
        await AtualizarElegibilidadeConvenioAsync(consulta, paciente, medico, ct);

        try
        {
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, MensagemAgendamento(consulta, atualizado: true), consulta);
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
        CancellationToken ct = default,string? esperadoSnapshot = null)
    {
        await using var tx=await _context.Database.BeginTransactionAsync(ct);
        await LockJornadaAsync(consultaId,ct);
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");
        if(esperadoSnapshot!=null && JornadaService.Snapshot(consulta)!=esperadoSnapshot)return Falha("A consulta mudou. Acesse sua conta para continuar.");

        if(await EmAtendimentoAsync(consultaId,ct))return Falha("O atendimento já foi iniciado. Procure a equipe.");
        if (!ConsultaStatus.PodeRemarcar(consulta.Status))
            return Falha("Essa consulta não pode mais ser remarcada.");

        await BloquearSlotAsync(consulta.MedicoId, data, horario, ct);

        if (!await HorarioDisponivelAsync(consulta.MedicoId, data, horario, consulta.Id, ct))
        {
            await tx.RollbackAsync(ct);
            return Falha("Esse novo horário já está ocupado ou indisponível.");
        }

        consulta.Revisao++;
        await ReiniciarAsync(consulta.Id,ct);
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
        CancellationToken ct = default,string? esperadoSnapshot = null)
    {
        await using var tx=await _context.Database.BeginTransactionAsync(ct);
        await LockJornadaAsync(consultaId,ct);
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");
        if(esperadoSnapshot!=null && JornadaService.Snapshot(consulta)!=esperadoSnapshot)return Falha("A consulta mudou. Acesse sua conta para continuar.");

        if (string.Equals(consulta.Status, ConsultaStatus.Confirmada, StringComparison.OrdinalIgnoreCase))
        {
            await RegistrarPresencaAsync(consulta.Id,ct);await _context.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return new(true, "Presença confirmada com sucesso.", consulta);
        }

        if (!ConsultaStatus.PodeConfirmar(consulta.Status))
            return Falha("Essa consulta não pode ser confirmada nesse estado.");

        await RegistrarPresencaAsync(consulta.Id,ct);
        consulta.Status = ConsultaStatus.Confirmada;
        consulta.ConfirmadaEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(true, "Consulta confirmada com sucesso.", consulta);
    }

    public async Task<ResultadoOperacaoConsulta> CancelarAsync(
        int consultaId,
        CancellationToken ct = default,string? esperadoSnapshot = null)
    {
        await using var tx=await _context.Database.BeginTransactionAsync(ct);
        await LockJornadaAsync(consultaId,ct);
        var consulta = await _context.Consultas.FirstOrDefaultAsync(c => c.Id == consultaId, ct);
        if (consulta is null)
            return Falha("Consulta não encontrada.");
        if(esperadoSnapshot!=null && JornadaService.Snapshot(consulta)!=esperadoSnapshot)return Falha("A consulta mudou. Acesse sua conta para continuar.");

        if (string.Equals(consulta.Status, ConsultaStatus.Cancelada, StringComparison.OrdinalIgnoreCase))
            return new(true, "A consulta já está cancelada.", consulta);

        if(await EmAtendimentoAsync(consultaId,ct))return Falha("O atendimento já foi iniciado. Procure a equipe.");
        if (!ConsultaStatus.PodeCancelar(consulta.Status))
            return Falha("Essa consulta não pode ser cancelada nesse estado.");

        consulta.Status = ConsultaStatus.Cancelada;
        consulta.CanceladaEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if(_encaixes!=null)try{await _encaixes.ReservarAsync(ct,consulta.MedicoId,consulta.Data,consulta.Horario);}catch(Exception e)when(e is not OperationCanceledException){_logger.LogError(e,"Cancelamento concluído; encaixe será reprocessado em segundo plano.");}
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

    private async Task RegistrarPresencaAsync(int id,CancellationToken ct){var j=await _context.Jornadas.SingleOrDefaultAsync(x=>x.ConsultaId==id,ct);if(j==null){j=new(){ConsultaId=id};_context.Jornadas.Add(j);}j.PresencaConfirmadaEm??=DateTime.UtcNow;j.AtualizadoEm=DateTime.UtcNow;}
    public static bool Sobrepoe(string a,int duracaoA,string b,int duracaoB)=>TimeSpan.TryParse(a,out var x)&&TimeSpan.TryParse(b,out var y)&&x<y.Add(TimeSpan.FromMinutes(duracaoB))&&y<x.Add(TimeSpan.FromMinutes(duracaoA));
    private sealed record Ocupado(DateTime Data,string Hora,int Duracao);
    private sealed record Ocupacao(int Duracao,List<Ocupado> Itens) {
        public bool Livre(DateTime data,string hora)=>TimeSpan.TryParse(hora,out var inicio)&&inicio.Add(TimeSpan.FromMinutes(Duracao))<=TimeSpan.FromDays(1)&&!Itens.Any(c=>c.Data.Date==data.Date&&Sobrepoe(hora,Duracao,c.Hora,c.Duracao));
    }
    private async Task<Ocupacao> CarregarOcupacaoAsync(int medicoId,DateTime inicio,DateTime fim,int? ignorar,Guid? ofertaId,CancellationToken ct) {
        var duracao=await _context.Medicos.Where(m=>m.Id==medicoId).Select(m=>m.DuracaoPadraoMinutos).FirstOrDefaultAsync(ct);duracao=Math.Clamp(duracao,5,240);
        var consultas=await _context.Consultas.AsNoTracking().Where(c=>c.MedicoId==medicoId&&c.Data.Date>=inicio.Date&&c.Data.Date<=fim.Date&&c.Status!=ConsultaStatus.Cancelada&&(!ignorar.HasValue||c.Id!=ignorar)).Select(c=>new{c.Data,c.Horario,Duracao=_context.Jornadas.Where(j=>j.ConsultaId==c.Id).Select(j=>(int?)j.Duracao).FirstOrDefault()??c.DuracaoMinutos}).ToListAsync(ct);
        var ofertas=await _context.Ofertas.AsNoTracking().Where(o=>o.MedicoId==medicoId&&o.Data>=inicio.Date&&o.Data<=fim.Date&&o.Estado=="Aberta"&&o.ExpiraEm>DateTime.UtcNow&&(!ofertaId.HasValue||o.Id!=ofertaId)).Select(o=>new{o.Data,o.Horario}).ToListAsync(ct);
        return new(duracao,consultas.Select(c=>new Ocupado(c.Data,c.Horario,c.Duracao)).Concat(ofertas.Select(o=>new Ocupado(o.Data,o.Horario,duracao))).ToList());
    }
    private async Task<bool> IntervaloLivreAsync(int medicoId,DateTime data,string hora,int? ignorar,Guid? ofertaId,CancellationToken ct)=>(await CarregarOcupacaoAsync(medicoId,data,data,ignorar,ofertaId,ct)).Livre(data,hora);
    private Task<bool> EmAtendimentoAsync(int id,CancellationToken ct)=>_context.Jornadas.AnyAsync(x=>x.ConsultaId==id&&x.InicioEm!=null&&x.FimEm==null,ct);
    private async Task LockJornadaAsync(int id,CancellationToken ct){if(!_context.Database.IsRelational())return;var key="jornada:"+id;await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0));",ct);var c=_context.Consultas.Local.FirstOrDefault(c=>c.Id==id);if(c!=null)await _context.Entry(c).ReloadAsync(ct);}
    private async Task ReiniciarAsync(int id,CancellationToken ct){var j=await _context.Jornadas.SingleOrDefaultAsync(x=>x.ConsultaId==id,ct);if(j==null)return;j.SalaId=null;j.PresencaConfirmadaEm=null;j.CadastroEm=null;j.CheckInEm=null;j.ChegadaEm=null;j.ConsentimentoEm=null;j.ConsentimentoVersao=null;j.PagamentoConferido=false;j.Revisao++;j.AtualizadoEm=DateTime.UtcNow;}
    private async Task BloquearSlotAsync(
        int medicoId,
        DateTime data,
        string horario,
        CancellationToken ct)
    {
        // Os testes e previews InMemory não oferecem advisory locks SQL. Em
        // PostgreSQL, a proteção transacional continua habilitada normalmente.
        if (!_context.Database.IsRelational())
            return;

        var dia = $"mksan-dia:{medicoId}:{data:yyyy-MM-dd}";
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({dia}, 0));",ct);
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

    private async Task AtualizarElegibilidadeConvenioAsync(
        Consulta consulta, Paciente paciente, Medico medico, CancellationToken ct)
    {
        if (!string.Equals(consulta.TipoPagamento, TipoPagamentoConsulta.Convenio, StringComparison.OrdinalIgnoreCase))
        {
            consulta.StatusElegibilidadeConvenio = ElegibilidadeConvenioStatus.NaoAplicavel;
            consulta.ObservacaoElegibilidadeConvenio = null;
            consulta.ProtocoloAutorizacaoConvenio = null;
            consulta.ElegibilidadeConvenioAtualizadaEm = null;
            consulta.ElegibilidadeConvenioAtualizadaPorUsuarioId = null;
            return;
        }

        ResultadoElegibilidadeConvenio resultado;
        if (medico.EspecialidadeId.HasValue)
        {
            resultado = await _elegibilidadeConvenio.AvaliarAsync(paciente, medico.EspecialidadeId, ct);
        }
        else
        {
            resultado = new(true, false, true,
                "Consulta confirmada; é necessária conferência manual da especialidade e da cobertura.",
                ElegibilidadeConvenioStatus.PendenteValidacao, false,
                "O psicólogo não possui especialidade vinculada. Confirmar especialidade, credenciamento e elegibilidade com a operadora.");
        }

        consulta.StatusElegibilidadeConvenio = resultado.StatusConsulta;
        consulta.ObservacaoElegibilidadeConvenio = Limitar(resultado.ObservacaoConsulta ?? resultado.Mensagem, 600);
        consulta.ProtocoloAutorizacaoConvenio = null;
        // A matriz local não confirma a elegibilidade do beneficiário na operadora.
        consulta.ElegibilidadeConvenioAtualizadaEm = null;
        consulta.ElegibilidadeConvenioAtualizadaPorUsuarioId = null;
    }

    private static string MensagemAgendamento(Consulta consulta, bool atualizado = false)
    {
        var inicio = atualizado ? "Consulta atualizada com sucesso." : "Consulta agendada e confirmada com sucesso.";
        if (consulta.TipoPagamento != TipoPagamentoConsulta.Convenio) return inicio;

        return consulta.StatusElegibilidadeConvenio switch
        {
            ElegibilidadeConvenioStatus.PendenteValidacao => inicio + " A cobertura do convênio precisa ser conferida pela equipe antes do atendimento.",
            ElegibilidadeConvenioStatus.CoberturaNaMatriz => inicio + " A matriz da clínica indica cobertura; a equipe ainda precisa confirmar a elegibilidade do beneficiário junto à operadora.",
            ElegibilidadeConvenioStatus.AguardandoAutorizacao => inicio + " A autorização do convênio está pendente e precisa ser acompanhada antes do atendimento.",
            _ => inicio
        };
    }

    private static ResultadoOperacaoConsulta Falha(string mensagem) =>
        new(false, mensagem);
}
