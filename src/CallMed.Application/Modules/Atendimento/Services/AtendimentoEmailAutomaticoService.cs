using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoEmailAutomaticoService(
    MKSANContext context, AtendimentoConversaService conversas,
    AtendimentoOrquestradorService orquestrador, IOptions<AtendimentoEmailOptions> options,
    ILogger<AtendimentoEmailAutomaticoService> logger)
{
    public async Task<int> ProcessarPendenciasAsync(CancellationToken ct = default)
    {
        if (!options.Value.EnviarRespostasAutomaticamente || !options.Value.RetomarPendencias) return 0;
        var agora = DateTime.UtcNow;
        var inicio = agora.AddHours(-Math.Clamp(options.Value.JanelaPendenciasHoras, 1, 72));
        var retryAntes = agora.AddMinutes(-1);
        var interrompidaAntes = agora.AddMinutes(-10);
        // Seleciona a entrada pendente antes do limite do lote. Rascunhos prontos,
        // respostas enviadas e mensagens antigas não podem bloquear a fila recente.
        var lista = await context.MensagensAtendimento.Include(e => e.Conversa)
            .Where(e => e.Direcao == DirecaoMensagemAtendimento.Entrada && e.Autor == AutorMensagemAtendimento.Paciente && e.CriadoEm >= inicio &&
                !context.MensagensAtendimento.Any(n => n.ConversaAtendimentoId == e.ConversaAtendimentoId && n.Id > e.Id &&
                    n.Direcao == DirecaoMensagemAtendimento.Entrada && n.Autor == AutorMensagemAtendimento.Paciente) &&
                !context.MensagensAtendimento.Any(n => n.ConversaAtendimentoId == e.ConversaAtendimentoId && n.Id > e.Id &&
                    n.Direcao == DirecaoMensagemAtendimento.Saida && n.Status != StatusMensagemAtendimento.Ignorada &&
                    (n.Autor == AutorMensagemAtendimento.Assistente || n.Autor == AutorMensagemAtendimento.Funcionario)) &&
                e.Conversa != null && e.Conversa.Ativa && e.Conversa.Canal == CanalAtendimento.Email && e.Conversa.ResponsavelUsuarioId == null &&
                ((e.Conversa.Modo == ModoAtendimento.IA &&
                  (e.Conversa.IaEntradaId != e.Id ||
                   (e.Conversa.IaPoliticaVersao != AtendimentoEmailOptions.PoliticaManual && e.Conversa.IaTentativas < 3 &&
                    (e.Conversa.IaEstado == null || e.Conversa.IaEstado == "pendente" ||
                     (e.Conversa.IaEstado == "falha" && e.Conversa.IaAtualizadoEm < retryAntes) ||
                     ((e.Conversa.IaEstado == "analisando" || e.Conversa.IaEstado == "gerando") && e.Conversa.IaAtualizadoEm < interrompidaAntes))))) ||
                 (e.Conversa.Modo == ModoAtendimento.Humano && e.Conversa.IaTentativas < 3 &&
                  e.Conversa.IaPoliticaVersao != AtendimentoEmailOptions.PoliticaAtual && e.Conversa.IaPoliticaVersao != AtendimentoEmailOptions.PoliticaManual &&
                  (e.Conversa.IaEstado == "equipe" || e.Conversa.IaEstado == "falha"))))
            .OrderBy(e => e.Id).Take(20).ToListAsync(ct);
        var tratados = 0;
        foreach (var item in lista)
        {
            var conversa = item.Conversa!;
            var entrada = await conversas.ObterUltimaEntradaPendenteAsync(conversa.Id, ct);
            if (entrada is null || entrada.CriadoEm < inicio) continue;
            // Uma tomada humana intencional nunca é desfeita pelo processamento automático.
            if (AtendimentoIntencao.EhPedidoAtendimentoHumano(entrada.Texto) && conversa.Modo == ModoAtendimento.Humano)
            {
                conversa.IaPoliticaVersao = AtendimentoEmailOptions.PoliticaAtual;
                await context.SaveChangesAsync(ct);
                continue;
            }
            try
            {
                await orquestrador.RetomarEmailAutomaticamenteAsync(conversa, ct);
                tratados++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao retomar pendência de e-mail {ConversaId}.", conversa.Id); }
        }
        return tratados;
    }
}

public sealed class AtendimentoEmailAutomaticoBackgroundService(
    IServiceScopeFactory scopes, ILogger<AtendimentoEmailAutomaticoBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AtendimentoEmailAutomaticoService>().ProcessarPendenciasAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Falha no ciclo de retomada automática dos e-mails."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
