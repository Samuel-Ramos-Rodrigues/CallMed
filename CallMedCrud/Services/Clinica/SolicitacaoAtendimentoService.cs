using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Clinica;

public sealed class SolicitacaoAtendimentoService
{
    private readonly MKSANContext _context;
    private readonly ConvenioElegibilidadeService _elegibilidade;
    private readonly AuditoriaService _auditoria;

    public SolicitacaoAtendimentoService(
        MKSANContext context,
        ConvenioElegibilidadeService elegibilidade,
        AuditoriaService auditoria)
    {
        _context = context;
        _elegibilidade = elegibilidade;
        _auditoria = auditoria;
    }

    public async Task<SolicitacaoAtendimento> CriarAsync(
        CanalAtendimento canal,
        int? pacienteId,
        int? especialidadeId,
        int? medicoId,
        DateTime? dataPreferida,
        string? periodo,
        string? observacao,
        string? nomeContato = null,
        string? telefoneContato = null,
        string? emailContato = null,
        long? conversaId = null,
        CancellationToken ct = default)
    {
        Paciente? paciente = null;
        if (pacienteId.HasValue)
        {
            paciente = await _context.Pacientes.FirstOrDefaultAsync(x => x.Id == pacienteId && x.Ativo, ct)
                ?? throw new InvalidOperationException("Paciente inválido ou inativo.");
        }

        if (especialidadeId.HasValue &&
            !await _context.Especialidades.AsNoTracking().AnyAsync(x => x.Id == especialidadeId && x.Ativo, ct))
            throw new InvalidOperationException("Especialidade inválida ou inativa.");

        if (medicoId.HasValue &&
            !await _context.Medicos.AsNoTracking().AnyAsync(x => x.Id == medicoId && x.Ativo, ct))
            throw new InvalidOperationException("Médico inválido ou inativo.");

        var item = new SolicitacaoAtendimento
        {
            PacienteId = pacienteId,
            EspecialidadeId = especialidadeId,
            MedicoId = medicoId,
            ConversaAtendimentoId = conversaId,
            Canal = canal,
            Status = StatusSolicitacaoAtendimento.Nova,
            NomeContato = Limitar(nomeContato ?? paciente?.Nome, 160),
            TelefoneContato = Limitar(telefoneContato ?? paciente?.Telefone, 40),
            EmailContato = Limitar(emailContato ?? paciente?.Email, 256),
            ConvenioInformado = Limitar(paciente?.NomeConvenio, 120),
            DataPreferida = dataPreferida?.Date,
            PeriodoPreferido = NormalizarPeriodo(periodo),
            Observacao = Limitar(observacao, 1200),
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        _context.SolicitacoesAtendimento.Add(item);
        await _context.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("Criar", "Solicitação", item.Id, $"Solicitação criada via {canal}.", novo: new { item.Canal, item.Status, item.PacienteId, item.EspecialidadeId }, ct: ct);
        return item;
    }

    public async Task<SolicitacaoAtendimento?> ObterOuCriarDaConversaAsync(
        ConversaAtendimento conversa,
        string? textoInicial,
        CancellationToken ct = default)
    {
        var existente = await _context.SolicitacoesAtendimento
            .FirstOrDefaultAsync(x => x.ConversaAtendimentoId == conversa.Id &&
                x.Status != StatusSolicitacaoAtendimento.Cancelada &&
                x.Status != StatusSolicitacaoAtendimento.Encerrada &&
                x.Status != StatusSolicitacaoAtendimento.Confirmada, ct);
        if (existente is not null) return existente;

        return await CriarAsync(
            conversa.Canal,
            conversa.PacienteId,
            null,
            null,
            null,
            "Qualquer",
            textoInicial,
            conversa.Paciente?.Nome,
            conversa.Paciente?.Telefone,
            conversa.Paciente?.Email,
            conversa.Id,
            ct);
    }

    public async Task<(bool Sucesso, string Mensagem)> TriarAsync(
        int id,
        int? pacienteId,
        int? especialidadeId,
        int? medicoId,
        string? pendencia,
        string? justificativaLiberacao,
        bool atendimentoParticular,
        bool liberarSemMatriz,
        string? responsavelUsuarioId,
        CancellationToken ct = default)
    {
        var item = await _context.SolicitacoesAtendimento
            .Include(x => x.Paciente)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return (false, "Solicitação não encontrada.");

        var anterior = new { item.Status, item.PacienteId, item.EspecialidadeId, item.MedicoId, item.ElegivelConvenio, item.PendenciaTriagem, item.JustificativaLiberacao };

        if (pacienteId.HasValue)
        {
            var paciente = await _context.Pacientes.FirstOrDefaultAsync(x => x.Id == pacienteId && x.Ativo, ct);
            if (paciente is null) return (false, "Paciente inválido ou inativo.");
            item.PacienteId = paciente.Id;
            item.Paciente = paciente;
            item.NomeContato = paciente.Nome;
            item.TelefoneContato = paciente.Telefone;
            item.EmailContato = paciente.Email;
            item.ConvenioInformado = paciente.NomeConvenio;
        }

        if (especialidadeId.HasValue &&
            !await _context.Especialidades.AsNoTracking().AnyAsync(x => x.Id == especialidadeId && x.Ativo, ct))
            return (false, "Especialidade inválida ou inativa.");
        item.EspecialidadeId = especialidadeId;

        if (medicoId.HasValue)
        {
            var medico = await _context.Medicos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == medicoId && x.Ativo, ct);
            if (medico is null) return (false, "Médico inválido ou inativo.");
            if (especialidadeId.HasValue && medico.EspecialidadeId != especialidadeId)
                return (false, "O médico selecionado não pertence à especialidade informada.");
            item.MedicoId = medicoId;
        }
        else
        {
            item.MedicoId = null;
        }

        item.Status = StatusSolicitacaoAtendimento.EmTriagem;
        item.ResponsavelUsuarioId = responsavelUsuarioId;
        item.TriadaEm = DateTime.UtcNow;
        item.AtualizadoEm = DateTime.UtcNow;
        item.PendenciaTriagem = Limitar(pendencia, 600);
        item.JustificativaLiberacao = null;

        if (item.Paciente is not null && item.EspecialidadeId.HasValue)
        {
            var elegibilidade = await _elegibilidade.AvaliarAsync(item.Paciente, item.EspecialidadeId, ct);
            if (atendimentoParticular || !item.Paciente.TemConvenio)
            {
                item.ConvenioInformado = "Particular";
                item.ElegivelConvenio = null;
            }
            else if (!elegibilidade.PossuiConvenioValido)
            {
                item.ConvenioInformado = item.Paciente.NomeConvenio;
                item.ElegivelConvenio = false;
                item.PendenciaTriagem = "Convênio vencido ou incompleto. Atualize o cadastro ou marque 'Atendimento particular' para continuar.";
            }
            else if (!elegibilidade.RegrasConfiguradas)
            {
                item.ConvenioInformado = item.Paciente.NomeConvenio;
                if (liberarSemMatriz)
                {
                    if (string.IsNullOrWhiteSpace(justificativaLiberacao))
                    {
                        item.ElegivelConvenio = null;
                        item.PendenciaTriagem = "Informe a justificativa da liberação manual para continuar sem matriz de cobertura.";
                    }
                    else
                    {
                        item.ElegivelConvenio = true;
                        item.JustificativaLiberacao = Limitar(justificativaLiberacao, 600);
                        if (string.IsNullOrWhiteSpace(pendencia)) item.PendenciaTriagem = null;
                    }
                }
                else
                {
                    item.ElegivelConvenio = null;
                    item.PendenciaTriagem = "Não há matriz de cobertura cadastrada para este convênio. Cadastre a regra ou faça uma liberação manual justificada.";
                }
            }
            else
            {
                item.ConvenioInformado = item.Paciente.NomeConvenio;
                item.ElegivelConvenio = elegibilidade.Elegivel;
                if (!elegibilidade.Elegivel) item.PendenciaTriagem = elegibilidade.Mensagem;
                else if (string.IsNullOrWhiteSpace(pendencia)) item.PendenciaTriagem = null;
            }
        }

        if (item.PacienteId.HasValue && item.EspecialidadeId.HasValue && item.ElegivelConvenio != false && string.IsNullOrWhiteSpace(item.PendenciaTriagem))
            item.Status = StatusSolicitacaoAtendimento.BuscandoHorario;

        await _context.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("Triagem", "Solicitação", item.Id, "Triagem administrativa atualizada.", anterior, new { item.Status, item.PacienteId, item.EspecialidadeId, item.MedicoId, item.ElegivelConvenio, item.PendenciaTriagem, item.JustificativaLiberacao, atendimentoParticular, liberarSemMatriz }, ct);

        return (true, item.Status == StatusSolicitacaoAtendimento.BuscandoHorario
            ? "Triagem concluída. A solicitação está pronta para buscar um horário."
            : "Triagem salva. Revise as pendências antes de avançar.");
    }

    public async Task<SolicitacaoAtendimento> RegistrarAgendamentoDiretoAsync(
        CanalAtendimento canal,
        int pacienteId,
        int medicoId,
        int consultaId,
        string? observacao = null,
        CancellationToken ct = default)
    {
        var medico = await _context.Medicos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == medicoId, ct);

        var limite = DateTime.UtcNow.AddHours(-4);
        var especialidadeId = medico?.EspecialidadeId;
        var existente = await _context.SolicitacoesAtendimento
            .Where(x => x.PacienteId == pacienteId && x.CriadoEm >= limite &&
                x.Status != StatusSolicitacaoAtendimento.Cancelada &&
                x.Status != StatusSolicitacaoAtendimento.Encerrada &&
                x.Status != StatusSolicitacaoAtendimento.Confirmada &&
                (!especialidadeId.HasValue || x.EspecialidadeId == especialidadeId) &&
                (!x.MedicoId.HasValue || x.MedicoId == medicoId))
            .OrderByDescending(x => x.CriadoEm)
            .FirstOrDefaultAsync(ct);

        if (existente is null)
        {
            existente = await CriarAsync(
                canal, pacienteId, medico?.EspecialidadeId, medicoId, null, "Qualquer",
                observacao ?? "Agendamento concluído diretamente pelo canal de autoatendimento.", ct: ct);
        }

        await VincularConsultaAsync(existente.Id, consultaId, ct);
        return existente;
    }

    public static CanalAtendimento MapearCanal(string? canal) => (canal ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "whatsapp" => CanalAtendimento.WhatsApp,
        "sms" => CanalAtendimento.Sms,
        "email" or "e-mail" => CanalAtendimento.Email,
        "telefone" or "phone" => CanalAtendimento.Telefone,
        "presencial" => CanalAtendimento.Presencial,
        _ => CanalAtendimento.Web
    };

    public async Task VincularConsultaAsync(int solicitacaoId, int consultaId, CancellationToken ct = default)
    {
        var item = await _context.SolicitacoesAtendimento.FirstOrDefaultAsync(x => x.Id == solicitacaoId, ct);
        if (item is null) return;
        item.ConsultaId = consultaId;
        item.Status = StatusSolicitacaoAtendimento.Confirmada;
        item.ConfirmadaEm = DateTime.UtcNow;
        item.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("Vincular consulta", "Solicitação", item.Id, $"Consulta #{consultaId} vinculada à solicitação.", novo: new { item.ConsultaId, item.Status }, ct: ct);
    }

    public async Task AtualizarPorConsultaAsync(int consultaId, StatusSolicitacaoAtendimento status, CancellationToken ct = default)
    {
        var itens = await _context.SolicitacoesAtendimento
            .Where(x => x.ConsultaId == consultaId &&
                x.Status != StatusSolicitacaoAtendimento.Cancelada &&
                x.Status != StatusSolicitacaoAtendimento.Encerrada)
            .ToListAsync(ct);
        foreach (var item in itens)
        {
            var anterior = item.Status;
            item.Status = status;
            item.AtualizadoEm = DateTime.UtcNow;
            if (status is StatusSolicitacaoAtendimento.Cancelada or StatusSolicitacaoAtendimento.Encerrada)
                item.EncerradaEm = DateTime.UtcNow;
            await _auditoria.RegistrarAsync("Sincronizar consulta", "Solicitação", item.Id, $"Consulta #{consultaId}: status da solicitação alterado de {anterior} para {status}.", ct: ct);
        }
        if (itens.Count > 0) await _context.SaveChangesAsync(ct);
    }

    public async Task AtualizarStatusAsync(int id, StatusSolicitacaoAtendimento status, CancellationToken ct = default)
    {
        var item = await _context.SolicitacoesAtendimento.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) throw new InvalidOperationException("Solicitação não encontrada.");
        var anterior = item.Status;
        item.Status = status;
        item.AtualizadoEm = DateTime.UtcNow;
        if (status == StatusSolicitacaoAtendimento.AguardandoPaciente) item.AguardandoPacienteEm = DateTime.UtcNow;
        if (status == StatusSolicitacaoAtendimento.Encerrada || status == StatusSolicitacaoAtendimento.Cancelada) item.EncerradaEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("Status", "Solicitação", item.Id, $"Status alterado de {anterior} para {status}.", anterior, status, ct);
    }

    private static string NormalizarPeriodo(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "manha" or "manhã" => "Manhã",
        "tarde" => "Tarde",
        "noite" => "Noite",
        _ => "Qualquer"
    };

    private static string? Limitar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var t = valor.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
