using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

namespace MKSANCrud.Services.Clinica;

public sealed class LembreteConsultaBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteConsultaBackgroundService> _logger;
    public LembreteConsultaBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LembreteConsultaBackgroundService> logger) { _scopeFactory = scopeFactory; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        do { await ProcessarAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessarAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MKSANContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClinicaClock>();
            var conversas = scope.ServiceProvider.GetRequiredService<AtendimentoConversaService>();
            var envio = scope.ServiceProvider.GetRequiredService<AtendimentoEnvioService>();
            var agora = clock.Agora;
            var limite = agora.AddHours(26).Date.AddDays(1);
            var consultas = await db.Consultas.Include(c => c.Paciente).Include(c => c.Medico)
                .Where(c => c.Data.Date >= agora.Date && c.Data.Date <= limite && c.Status != ConsultaStatus.Cancelada && c.Status != ConsultaStatus.Realizada && c.Status != ConsultaStatus.Ausente)
                .ToListAsync(ct);

            foreach (var c in consultas)
            {
                if (c.Paciente is null || !TimeSpan.TryParse(c.Horario, out var hora)) continue;
                var quando = c.Data.Date.Add(hora);
                var faltam = quando - agora;
                var tipo24 = faltam > TimeSpan.FromHours(20) && faltam <= TimeSpan.FromHours(26) && c.Lembrete24hEnviadoEm == null;
                var tipo2 = faltam > TimeSpan.FromMinutes(60) && faltam <= TimeSpan.FromHours(3) && c.Lembrete2hEnviadoEm == null;
                if (!tipo24 && !tipo2) continue;

                if (string.Equals(c.Paciente.CanalPreferido, "Telefone", StringComparison.OrdinalIgnoreCase))
                {
                    var existeTarefa = await db.SolicitacoesAtendimento.AnyAsync(x =>
                        x.ConsultaId == c.Id &&
                        x.Canal == CanalAtendimento.Telefone &&
                        x.Status != StatusSolicitacaoAtendimento.Cancelada &&
                        x.Status != StatusSolicitacaoAtendimento.Encerrada, ct);

                    if (!existeTarefa)
                    {
                        db.SolicitacoesAtendimento.Add(new SolicitacaoAtendimento
                        {
                            PacienteId = c.PacienteId,
                            EspecialidadeId = c.Medico?.EspecialidadeId,
                            MedicoId = c.MedicoId,
                            ConsultaId = c.Id,
                            Canal = CanalAtendimento.Telefone,
                            Status = StatusSolicitacaoAtendimento.Nova,
                            NomeContato = c.Paciente.Nome,
                            TelefoneContato = c.Paciente.Telefone,
                            ConvenioInformado = c.Paciente.NomeConvenio,
                            Observacao = tipo24
                                ? $"Ligar para confirmar a consulta de amanhã ({c.Data:dd/MM} às {c.Horario})."
                                : $"Ligar sobre a consulta de hoje às {c.Horario}.",
                            CriadoEm = DateTime.UtcNow,
                            AtualizadoEm = DateTime.UtcNow
                        });
                    }

                    if (tipo24) c.Lembrete24hEnviadoEm = DateTime.UtcNow; else c.Lembrete2hEnviadoEm = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var destino = EscolherCanal(c.Paciente, envio);
                if (destino is null)
                {
                    if (!string.IsNullOrWhiteSpace(c.Paciente.Telefone))
                    {
                        var criouTarefa = await CriarTarefaTelefoneAsync(db, c, tipo24, ct);
                        if (criouTarefa)
                        {
                            if (tipo24) c.Lembrete24hEnviadoEm = DateTime.UtcNow; else c.Lembrete2hEnviadoEm = DateTime.UtcNow;
                            await db.SaveChangesAsync(ct);
                        }
                    }
                    continue;
                }
                var conversa = await conversas.ObterOuCriarAsync(destino.Value.Canal, destino.Value.Identificador, c.PacienteId, "Lembrete de consulta CallMed", ct: ct);
                var texto = tipo24
                    ? $"Olá, {c.Paciente.Nome}! Lembrete CallMed: sua consulta com {c.Medico?.Nome} é amanhã, {c.Data:dd/MM}, às {c.Horario}. Responda CONFIRMAR para confirmar presença, REMARCAR para escolher outro horário ou CANCELAR para liberar a vaga."
                    : $"Olá, {c.Paciente.Nome}! Sua consulta CallMed com {c.Medico?.Nome} será hoje às {c.Horario}. Se estiver tudo certo, responda CONFIRMAR. Se precisar, responda REMARCAR ou CANCELAR.";
                var msg = await envio.EnviarAsync(conversa, texto, AutorMensagemAtendimento.Sistema, ct: ct);
                if (msg.Status == StatusMensagemAtendimento.Enviada)
                {
                    if (tipo24) c.Lembrete24hEnviadoEm = DateTime.UtcNow; else c.Lembrete2hEnviadoEm = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Falha no serviço de lembretes de consulta."); }
    }
    private static async Task<bool> CriarTarefaTelefoneAsync(MKSANContext db, Consulta c, bool tipo24, CancellationToken ct)
    {
        if (c.Paciente is null || string.IsNullOrWhiteSpace(c.Paciente.Telefone))
            return false;

        var existeTarefa = await db.SolicitacoesAtendimento.AnyAsync(x =>
            x.ConsultaId == c.Id &&
            x.Canal == CanalAtendimento.Telefone &&
            x.Status != StatusSolicitacaoAtendimento.Cancelada &&
            x.Status != StatusSolicitacaoAtendimento.Encerrada, ct);

        if (!existeTarefa)
        {
            db.SolicitacoesAtendimento.Add(new SolicitacaoAtendimento
            {
                PacienteId = c.PacienteId,
                EspecialidadeId = c.Medico?.EspecialidadeId,
                MedicoId = c.MedicoId,
                ConsultaId = c.Id,
                Canal = CanalAtendimento.Telefone,
                Status = StatusSolicitacaoAtendimento.Nova,
                NomeContato = c.Paciente.Nome,
                TelefoneContato = c.Paciente.Telefone,
                EmailContato = c.Paciente.Email,
                ConvenioInformado = c.Paciente.NomeConvenio,
                Observacao = tipo24
                    ? $"Ligar para confirmar a consulta de amanhã ({c.Data:dd/MM} às {c.Horario})."
                    : $"Ligar sobre a consulta de hoje às {c.Horario}.",
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow
            });
        }

        return true;
    }

    private static (CanalAtendimento Canal, string Identificador)? EscolherCanal(Paciente paciente, AtendimentoEnvioService envio)
    {
        var preferido = paciente.CanalPreferido?.Trim();
        if (string.Equals(preferido, "SMS", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(paciente.Telefone) && envio.CanalConfigurado(CanalAtendimento.Sms))
            return (CanalAtendimento.Sms, paciente.Telefone);
        if (string.Equals(preferido, "Email", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(paciente.Email) && envio.CanalConfigurado(CanalAtendimento.Email))
            return (CanalAtendimento.Email, paciente.Email);
        if (string.Equals(preferido, "WhatsApp", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(paciente.Telefone) && envio.CanalConfigurado(CanalAtendimento.WhatsApp))
            return (CanalAtendimento.WhatsApp, paciente.Telefone);

        if (!string.IsNullOrWhiteSpace(paciente.Telefone) && envio.CanalConfigurado(CanalAtendimento.WhatsApp)) return (CanalAtendimento.WhatsApp, paciente.Telefone);
        if (!string.IsNullOrWhiteSpace(paciente.Telefone) && envio.CanalConfigurado(CanalAtendimento.Sms)) return (CanalAtendimento.Sms, paciente.Telefone);
        if (!string.IsNullOrWhiteSpace(paciente.Email) && envio.CanalConfigurado(CanalAtendimento.Email)) return (CanalAtendimento.Email, paciente.Email);
        return null;
    }

}
