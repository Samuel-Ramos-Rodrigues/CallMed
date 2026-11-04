using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Atendimento.Canais.Sms;

public sealed class HttpSmsAtendimentoService(
    MKSANContext context, AtendimentoConversaService conversas,
    AtendimentoOrquestradorService orquestrador, IOptions<SmsHttpOptions> options,
    ILogger<HttpSmsAtendimentoService> logger)
{
    public async Task RegistrarEntradaAsync(string telefone, string texto, string id, CancellationToken ct)
    {
        var conversa = await conversas.ObterOuCriarAsync(CanalAtendimento.Sms, telefone, ct: ct);
        // O prefixo separa os novos eventos dos históricos de outros provedores.
        await conversas.RegistrarEntradaAsync(conversa, texto, "httpsms:" + id, ct);
    }

    public async Task<int> ProcessarPendenciasAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled) return 0;
        var inicio = DateTime.UtcNow.AddHours(-72);
        var interrompidaAntes = DateTime.UtcNow.AddMinutes(-10);
        // A mensagem já está persistida quando o webhook recebe HTTP 200. Uma
        // reinicialização do Render não perde a fila nem depende de Task.Run.
        var pendentes = await context.MensagensAtendimento.Include(m => m.Conversa)
            .Where(m => m.Direcao == DirecaoMensagemAtendimento.Entrada && m.CriadoEm >= inicio &&
                m.MensagemExternaId != null && m.MensagemExternaId.StartsWith("httpsms:") &&
                m.Conversa != null && m.Conversa.Canal == CanalAtendimento.Sms && m.Conversa.Ativa &&
                m.Conversa.Modo == ModoAtendimento.IA && m.Conversa.ResponsavelUsuarioId == null &&
                (m.Conversa.IaEntradaId != m.Id || (m.Conversa.IaTentativas < 3 &&
                    (m.Conversa.IaEstado == null || m.Conversa.IaEstado == "pendente" ||
                     ((m.Conversa.IaEstado == "analisando" || m.Conversa.IaEstado == "gerando") && m.Conversa.IaAtualizadoEm < interrompidaAntes)))) &&
                !context.MensagensAtendimento.Any(n => n.ConversaAtendimentoId == m.ConversaAtendimentoId && n.Id > m.Id &&
                    n.Direcao == DirecaoMensagemAtendimento.Entrada) &&
                !context.MensagensAtendimento.Any(n => n.ConversaAtendimentoId == m.ConversaAtendimentoId && n.Id > m.Id &&
                    n.Direcao == DirecaoMensagemAtendimento.Saida && n.Status != StatusMensagemAtendimento.Ignorada &&
                    (n.Autor == AutorMensagemAtendimento.Assistente || n.Autor == AutorMensagemAtendimento.Funcionario)))
            .OrderBy(m => m.Id).Take(20).ToListAsync(ct);
        var tratados = 0;
        foreach (var entrada in pendentes)
        {
            try
            {
                await orquestrador.RetomarUltimaEntradaPendenteAsync(entrada.Conversa!, ct);
                tratados++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao processar SMS persistido. EntradaId={EntradaId}.", entrada.Id); }
        }
        return tratados;
    }

    public async Task<bool> AtualizarStatusAsync(string evento, string id, JsonElement dados, CancellationToken ct)
    {
        var mensagem = await context.MensagensAtendimento.Include(m => m.Conversa)
            .FirstOrDefaultAsync(m => m.MensagemExternaId == id && m.Direcao == DirecaoMensagemAtendimento.Saida &&
                m.Conversa != null && m.Conversa.Canal == CanalAtendimento.Sms, ct);
        // O callback pode chegar antes de salvarmos o ID retornado pelo envio.
        // Nesse caso, o controller retorna 503 para o httpSMS tentar novamente.
        if (mensagem is null)
            return !dados.TryGetProperty("request_id", out var requestId) || requestId.ValueKind != JsonValueKind.String ||
                requestId.GetString()?.StartsWith("callmed:", StringComparison.Ordinal) != true;
        if (mensagem.Status == StatusMensagemAtendimento.Entregue) return true;
        if (evento == "message.send.expired" && dados.TryGetProperty("is_final", out var final) && final.ValueKind == JsonValueKind.False)
            return true; // O próprio httpSMS ainda está tentando enviar.
        switch (evento)
        {
            case "message.phone.delivered":
                mensagem.Status = StatusMensagemAtendimento.Entregue;
                mensagem.Erro = null;
                mensagem.ReenvioBloqueado = false;
                break;
            case "message.phone.sent" when mensagem.Status != StatusMensagemAtendimento.Falhou:
                mensagem.Status = StatusMensagemAtendimento.Enviada;
                mensagem.Erro = null;
                break;
            case "message.send.failed":
            case "message.send.expired":
                mensagem.Status = StatusMensagemAtendimento.Falhou;
                mensagem.Erro = evento == "message.send.expired"
                    ? "O SMS expirou no httpSMS antes do envio. Confira se o celular está conectado."
                    : "O aplicativo httpSMS informou falha no envio. Confira permissões, chip e conexão no celular.";
                mensagem.ReenvioBloqueado = false;
                break;
        }
        mensagem.VersaoEnvio = Guid.NewGuid();
        if (mensagem.Status is StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue)
            mensagem.EnviadoEm ??= DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class HttpSmsAtendimentoBackgroundService(
    IServiceScopeFactory scopes, ILogger<HttpSmsAtendimentoBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<HttpSmsAtendimentoService>().ProcessarPendenciasAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Falha no ciclo de atendimento automático por httpSMS."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
