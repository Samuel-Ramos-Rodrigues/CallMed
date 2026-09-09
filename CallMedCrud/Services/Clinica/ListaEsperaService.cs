using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

namespace MKSANCrud.Services.Clinica;

public sealed class ListaEsperaService
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;
    private readonly AtendimentoConversaService _conversas;
    private readonly AtendimentoEnvioService _envio;
    private readonly EspecialidadeService _especialidades;
    private readonly ConvenioElegibilidadeService _elegibilidade;
    private readonly SolicitacaoAtendimentoService _solicitacoes;

    public ListaEsperaService(
        MKSANContext context,
        IClinicaClock clock,
        AtendimentoConversaService conversas,
        AtendimentoEnvioService envio,
        EspecialidadeService especialidades,
        ConvenioElegibilidadeService elegibilidade,
        SolicitacaoAtendimentoService solicitacoes)
    {
        _context = context;
        _clock = clock;
        _conversas = conversas;
        _envio = envio;
        _especialidades = especialidades;
        _elegibilidade = elegibilidade;
        _solicitacoes = solicitacoes;
    }

    public async Task<ListaEspera> AdicionarAsync(
        int pacienteId,
        int? medicoId,
        int? especialidadeId,
        DateTime? dataPreferida,
        string? periodo,
        string? observacao,
        CancellationToken ct = default)
    {
        if (!medicoId.HasValue && !especialidadeId.HasValue)
            throw new InvalidOperationException("Informe um médico ou uma especialidade.");
        if (medicoId.HasValue && especialidadeId.HasValue)
            throw new InvalidOperationException("Escolha um médico específico ou uma especialidade, não os dois.");

        var paciente = await _context.Pacientes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pacienteId && p.Ativo, ct);
        if (paciente is null)
            throw new InvalidOperationException("Paciente inválido ou inativo.");

        int? especialidadeEfetiva = especialidadeId;
        if (medicoId.HasValue)
        {
            var medico = await _context.Medicos.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == medicoId.Value && m.Ativo, ct);
            if (medico is null)
                throw new InvalidOperationException("Médico inválido ou inativo.");
            especialidadeEfetiva = medico.EspecialidadeId;
        }

        if (especialidadeId.HasValue && !await _context.Especialidades.AsNoTracking()
                .AnyAsync(e => e.Id == especialidadeId.Value && e.Ativo && e.Medicos.Any(m => m.Ativo), ct))
            throw new InvalidOperationException("Especialidade indisponível no momento.");

        // Se o paciente usa um convênio válido, não criamos uma espera que nunca poderá
        // ser convertida automaticamente em consulta. A matriz deve estar definida e cobrir a especialidade.
        if (especialidadeEfetiva.HasValue)
        {
            var elegibilidade = await _elegibilidade.AvaliarAsync(paciente, especialidadeEfetiva, ct);
            if (paciente.TemConvenio && !elegibilidade.PossuiConvenioValido)
                throw new InvalidOperationException("Seu convênio está vencido ou incompleto. Atualize o cadastro ou procure o atendimento para registrar a solicitação como particular.");
            if (elegibilidade.PossuiConvenioValido && !elegibilidade.RegrasConfiguradas)
                throw new InvalidOperationException("A cobertura deste convênio ainda precisa ser configurada antes de entrar na lista de espera para esta especialidade.");
            if (elegibilidade.PossuiConvenioValido && !elegibilidade.Elegivel)
                throw new InvalidOperationException(elegibilidade.Mensagem);
        }

        var dataNormalizada = dataPreferida?.Date;
        if (dataNormalizada.HasValue && dataNormalizada.Value < _clock.Hoje)
            throw new InvalidOperationException("A data preferida não pode estar no passado.");

        var duplicado = await _context.ListasEspera.AnyAsync(x =>
            x.Ativa &&
            x.PacienteId == pacienteId &&
            x.MedicoId == medicoId &&
            x.EspecialidadeId == especialidadeId &&
            x.DataPreferida == dataNormalizada,
            ct);
        if (duplicado)
            throw new InvalidOperationException("Já existe um pedido ativo igual na sua lista de espera.");

        var item = new ListaEspera
        {
            PacienteId = pacienteId,
            MedicoId = medicoId,
            EspecialidadeId = especialidadeId,
            DataPreferida = dataNormalizada,
            Periodo = NormalizarPeriodo(periodo),
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Ativa = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };
        _context.ListasEspera.Add(item);
        await _context.SaveChangesAsync(ct);
        return item;
    }

    public async Task<int> ProcessarNotificacoesAsync(CancellationToken ct = default)
    {
        var itens = await _context.ListasEspera
            .Include(x => x.Paciente)
            .Include(x => x.Medico)
            .Include(x => x.Especialidade)
            .Where(x => x.Ativa && (x.NotificadoEm == null || x.NotificadoEm < DateTime.UtcNow.AddHours(-12)))
            .OrderBy(x => x.CriadoEm)
            .Take(100)
            .ToListAsync(ct);
        var enviados = 0;

        foreach (var item in itens)
        {
            if (item.Paciente is null || !item.Paciente.Ativo)
                continue;

            var vaga = await BuscarVagaAsync(item, ct);
            if (vaga is null || item.UltimaDisponibilidadeId == vaga.Id)
                continue;

            // Pacientes que preferem telefone ou ficaram sem um canal digital configurado
            // viram uma tarefa operacional na mesma fila omnichannel da recepção.
            if (string.Equals(item.Paciente.CanalPreferido, "Telefone", StringComparison.OrdinalIgnoreCase))
            {
                if (await CriarTarefaTelefoneAsync(item, vaga, ct))
                {
                    MarcarComoNotificado(item, vaga);
                    enviados++;
                }
                continue;
            }

            var destino = EscolherCanal(item.Paciente);
            if (destino is null)
            {
                if (!string.IsNullOrWhiteSpace(item.Paciente.Telefone) &&
                    await CriarTarefaTelefoneAsync(item, vaga, ct))
                {
                    MarcarComoNotificado(item, vaga);
                    enviados++;
                }
                continue;
            }

            var conversa = await _conversas.ObterOuCriarAsync(
                destino.Value.Canal,
                destino.Value.Identificador,
                item.PacienteId,
                "Lista de espera CallMed",
                ct: ct);

            var texto =
                $"Olá, {item.Paciente.Nome}! Abriu uma vaga na sua lista de espera: " +
                $"{vaga.Medico?.Nome} — {_especialidades.CanonicalizarNome(vaga.Medico?.Especialidade ?? item.Especialidade?.Nome ?? string.Empty)}, " +
                $"dia {vaga.Data!.Value:dd/MM/yyyy} às {vaga.Horario}. " +
                "Entre no app para aceitar a vaga ou responda por aqui se precisar de ajuda. A vaga continua sujeita à disponibilidade.";

            var msg = await _envio.EnviarAsync(conversa, texto, AutorMensagemAtendimento.Sistema, ct: ct);
            if (msg.Status == StatusMensagemAtendimento.Enviada)
            {
                MarcarComoNotificado(item, vaga);
                enviados++;
            }
        }

        if (enviados > 0)
            await _context.SaveChangesAsync(ct);
        return enviados;
    }

    private async Task<bool> CriarTarefaTelefoneAsync(ListaEspera item, Disponibilidade vaga, CancellationToken ct)
    {
        if (item.Paciente is null || string.IsNullOrWhiteSpace(item.Paciente.Telefone))
            return false;

        var marcador = $"lista de espera #{item.Id}";
        var existeTarefa = await _context.SolicitacoesAtendimento.AsNoTracking().AnyAsync(x =>
            x.PacienteId == item.PacienteId &&
            x.Canal == CanalAtendimento.Telefone &&
            x.Observacao != null && x.Observacao.Contains(marcador) &&
            x.Status != StatusSolicitacaoAtendimento.Cancelada &&
            x.Status != StatusSolicitacaoAtendimento.Encerrada &&
            x.Status != StatusSolicitacaoAtendimento.Confirmada,
            ct);

        if (existeTarefa)
            return true;

        await _solicitacoes.CriarAsync(
            CanalAtendimento.Telefone,
            item.PacienteId,
            item.EspecialidadeId ?? vaga.Medico?.EspecialidadeId,
            item.MedicoId ?? vaga.MedicoId,
            vaga.Data?.Date,
            item.Periodo,
            $"Ligar para oferecer a vaga da {marcador}: {vaga.Data:dd/MM/yyyy} às {vaga.Horario}.",
            item.Paciente.Nome,
            item.Paciente.Telefone,
            item.Paciente.Email,
            ct: ct);

        return true;
    }

    private static void MarcarComoNotificado(ListaEspera item, Disponibilidade vaga)
    {
        item.NotificadoEm = DateTime.UtcNow;
        item.UltimaDisponibilidadeId = vaga.Id;
        item.AtualizadoEm = DateTime.UtcNow;
    }

    private (CanalAtendimento Canal, string Identificador)? EscolherCanal(Paciente paciente)
    {
        var preferido = paciente.CanalPreferido?.Trim();
        if (string.Equals(preferido, "SMS", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(paciente.Telefone) && _envio.CanalConfigurado(CanalAtendimento.Sms))
            return (CanalAtendimento.Sms, paciente.Telefone);
        if (string.Equals(preferido, "Email", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(paciente.Email) && _envio.CanalConfigurado(CanalAtendimento.Email))
            return (CanalAtendimento.Email, paciente.Email);
        if (string.Equals(preferido, "WhatsApp", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(paciente.Telefone) && _envio.CanalConfigurado(CanalAtendimento.WhatsApp))
            return (CanalAtendimento.WhatsApp, paciente.Telefone);

        if (!string.IsNullOrWhiteSpace(paciente.Telefone) && _envio.CanalConfigurado(CanalAtendimento.WhatsApp))
            return (CanalAtendimento.WhatsApp, paciente.Telefone);
        if (!string.IsNullOrWhiteSpace(paciente.Telefone) && _envio.CanalConfigurado(CanalAtendimento.Sms))
            return (CanalAtendimento.Sms, paciente.Telefone);
        if (!string.IsNullOrWhiteSpace(paciente.Email) && _envio.CanalConfigurado(CanalAtendimento.Email))
            return (CanalAtendimento.Email, paciente.Email);
        return null;
    }

    private async Task<Disponibilidade?> BuscarVagaAsync(ListaEspera item, CancellationToken ct)
    {
        if (item.Paciente is null)
            return null;

        var inicio = item.DataPreferida?.Date ?? _clock.Hoje;
        var fim = item.DataPreferida?.Date ?? _clock.Hoje.AddDays(90);
        var query = _context.Disponibilidades
            .AsNoTracking()
            .Include(d => d.Medico)
            .Where(d =>
                d.Ativo &&
                d.Data.HasValue &&
                d.Data.Value.Date >= inicio &&
                d.Data.Value.Date <= fim &&
                d.Medico != null &&
                d.Medico.Ativo);

        if (item.MedicoId.HasValue)
            query = query.Where(d => d.MedicoId == item.MedicoId.Value);
        else if (item.EspecialidadeId.HasValue)
            query = query.Where(d => d.Medico!.EspecialidadeId == item.EspecialidadeId.Value);

        var vagas = await query
            .OrderBy(d => d.Data)
            .ThenBy(d => d.Horario)
            .Take(200)
            .ToListAsync(ct);

        var ocupados = await _context.Consultas.AsNoTracking()
            .Where(c =>
                c.Data.Date >= inicio &&
                c.Data.Date <= fim &&
                c.Status != ConsultaStatus.Cancelada)
            .Select(c => new { c.MedicoId, c.Data, c.Horario })
            .ToListAsync(ct);

        var set = ocupados
            .Select(c => $"{c.MedicoId}|{c.Data:yyyy-MM-dd}|{c.Horario}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var vaga in vagas)
        {
            if (!PeriodoAceito(vaga.Horario, item.Periodo) ||
                set.Contains($"{vaga.MedicoId}|{vaga.Data!.Value:yyyy-MM-dd}|{vaga.Horario}"))
                continue;

            var especialidadeId = vaga.Medico?.EspecialidadeId;
            if (especialidadeId.HasValue)
            {
                var elegibilidade = await _elegibilidade.AvaliarAsync(item.Paciente, especialidadeId, ct);
                if ((item.Paciente.TemConvenio && !elegibilidade.PossuiConvenioValido) ||
                    (elegibilidade.PossuiConvenioValido && (!elegibilidade.RegrasConfiguradas || !elegibilidade.Elegivel)))
                    continue;
            }

            return vaga;
        }

        return null;
    }

    private static string NormalizarPeriodo(string? p) => p?.Trim().ToLowerInvariant() switch
    {
        "manha" or "manhã" => "Manhã",
        "tarde" => "Tarde",
        "noite" => "Noite",
        _ => "Qualquer"
    };

    private static bool PeriodoAceito(string horario, string periodo)
    {
        if (periodo == "Qualquer" || !TimeSpan.TryParse(horario, out var h))
            return true;
        return periodo switch
        {
            "Manhã" => h < TimeSpan.FromHours(12),
            "Tarde" => h >= TimeSpan.FromHours(12) && h < TimeSpan.FromHours(18),
            "Noite" => h >= TimeSpan.FromHours(18),
            _ => true
        };
    }
}
