using Microsoft.Extensions.Options;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento;

namespace MKSANCrud.Services.Atendimento.Canais.Email;

/// <summary>
/// Lê mensagens novas do Gmail e cria rascunhos de resposta da IA na Central.
/// Nenhum rascunho é enviado sem aprovação explícita da equipe.
/// </summary>
public sealed class GmailInboxPollingService : BackgroundService
{
    private readonly GmailApiClient _gmail;
    private readonly IServiceScopeFactory _scopes;
    private readonly GmailInboxOptions _options;
    private readonly ILogger<GmailInboxPollingService> _logger;
    private bool _avisouInativo;

    public GmailInboxPollingService(
        GmailApiClient gmail,
        IServiceScopeFactory scopes,
        IOptions<GmailInboxOptions> options,
        ILogger<GmailInboxPollingService> logger)
    {
        _gmail = gmail;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Dá tempo para a aplicação concluir a inicialização do banco e dos patches.
        await Task.Delay(TimeSpan.FromSeconds(12), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_gmail.Configurado)
                {
                    _avisouInativo = false;
                    await ProcessarCicloAsync(stoppingToken);
                }
                else if (!_avisouInativo)
                {
                    _logger.LogInformation("Leitor do Gmail inativo: configure GmailInbox__Enabled, MailboxEmail, StartAfterUtc e credenciais OAuth no servidor.");
                    _avisouInativo = true;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo de leitura do Gmail. As mensagens não processadas continuarão disponíveis para nova tentativa.");
            }

            var intervalo = TimeSpan.FromSeconds(Math.Clamp(_options.PollIntervalSeconds, 30, 900));
            try { await Task.Delay(intervalo, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ProcessarCicloAsync(CancellationToken ct)
    {
        var ids = await _gmail.ListarMensagensPendentesAsync(ct);
        if (ids.Count == 0) return;

        var mensagens = new List<GmailInboxMessage>(ids.Count);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var mensagem = await _gmail.ObterMensagemAsync(id, ct);
            if (mensagem is not null) mensagens.Add(mensagem);
        }

        // Processar em ordem cronológica melhora o contexto quando chegam várias respostas juntas.
        _options.TryGetStartAfterUtc(out var startAfterUtc);
        var startAfterUnixMs = startAfterUtc.ToUnixTimeMilliseconds();
        foreach (var mensagem in mensagens.OrderBy(m => m.InternalDateUnixMs).ThenBy(m => m.Id, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();

            // Gmail usa filtros por segundo; rejeite qualquer mensagem anterior ao ponto de ativação exato.
            // Marcá-la como processada evita que mensagens antigas no mesmo segundo sejam consultadas para sempre.
            if (mensagem.InternalDateUnixMs > 0 && mensagem.InternalDateUnixMs < startAfterUnixMs)
            {
                await _gmail.MarcarComoProcessadaAsync(mensagem.Id, ct);
                continue;
            }

            if (mensagem.IsAutomated || string.IsNullOrWhiteSpace(mensagem.SenderEmail) || string.IsNullOrWhiteSpace(mensagem.Body))
            {
                _logger.LogInformation("Mensagem Gmail ignorada por ser automática, sem remetente válido ou sem corpo útil. GmailMessageId={MessageId}; Motivo={Motivo}.", mensagem.Id, mensagem.AutomatedReason ?? "sem corpo útil");
                await _gmail.MarcarComoProcessadaAsync(mensagem.Id, ct);
                continue;
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var orquestrador = scope.ServiceProvider.GetRequiredService<AtendimentoOrquestradorService>();
                await orquestrador.ProcessarEntradaEmailComoRascunhoAsync(
                    new CanalMensagemEntrada
                    {
                        Canal = CanalAtendimento.Email,
                        Identificador = mensagem.SenderEmail,
                        Texto = mensagem.Body,
                        Assunto = mensagem.Subject,
                        MensagemExternaId = "gmail:" + mensagem.Id
                    },
                    ct);

                await _gmail.MarcarComoProcessadaAsync(mensagem.Id, ct);
                _logger.LogInformation("Mensagem Gmail registrada na Central para revisão humana. GmailMessageId={MessageId}.", mensagem.Id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Não marcar como processada: o próximo ciclo poderá tentar novamente.
                _logger.LogError(ex, "Falha ao registrar mensagem recebida do Gmail. GmailMessageId={MessageId}.", mensagem.Id);
            }
        }
    }
}
