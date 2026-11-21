using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
namespace MKSANCrud.Services.Atendimento.Canais.WhatsApp;

public sealed class WhatsAppPendingBackgroundService(IServiceScopeFactory scopes, ILogger<WhatsAppPendingBackgroundService> logger) : BackgroundService
{
    public static async Task<int> ProcessarAsync(MKSANContext db, AtendimentoOrquestradorService orchestrator, CancellationToken ct)
    {
        var stale = DateTime.UtcNow.AddMinutes(-3); var since = DateTime.UtcNow.AddHours(-72);
        var rows = await db.MensagensAtendimento.Include(m => m.Conversa).Where(m =>
            m.Direcao == DirecaoMensagemAtendimento.Entrada && m.CriadoEm >= since && m.Conversa != null &&
            m.Conversa.Canal == CanalAtendimento.WhatsApp && m.Conversa.Ativa && m.Conversa.Modo == ModoAtendimento.IA && m.Conversa.ResponsavelUsuarioId == null &&
            (m.Conversa.IaEntradaId != m.Id || m.Conversa.IaTentativas < 3 && (m.Conversa.IaEstado == null || m.Conversa.IaEstado == "pendente" || m.Conversa.IaEstado == "falha" ||
                (m.Conversa.IaEstado == "analisando" || m.Conversa.IaEstado == "gerando") && m.Conversa.IaAtualizadoEm < stale)) &&
            !db.MensagensAtendimento.Any(n => n.ConversaAtendimentoId == m.ConversaAtendimentoId && n.Id > m.Id &&
                (n.Direcao == DirecaoMensagemAtendimento.Entrada || n.Direcao == DirecaoMensagemAtendimento.Saida && n.Status != StatusMensagemAtendimento.Ignorada && (n.Autor == AutorMensagemAtendimento.Assistente || n.Autor == AutorMensagemAtendimento.Funcionario))))
            .OrderBy(m => m.Id).Take(20).ToListAsync(ct);
        foreach (var m in rows) await orchestrator.RetomarUltimaEntradaPendenteAsync(m.Conversa!, ct);
        return rows.Count;
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var proximoRegistro=DateTime.MinValue;
        try { while (await timer.WaitForNextTickAsync(ct)) {
            try { using var scope = scopes.CreateScope(); var sp = scope.ServiceProvider;
                if (sp.GetRequiredService<IOptions<EvolutionWhatsAppOptions>>().Value.Enabled) {
                    await ProcessarAsync(sp.GetRequiredService<MKSANContext>(), sp.GetRequiredService<AtendimentoOrquestradorService>(), ct);
                    if(DateTime.UtcNow>=proximoRegistro){await MKSANCrud.Services.Clinica.OperacaoService.RegistrarAsync(sp,"whatsapp-pendencias",true,ct);proximoRegistro=DateTime.UtcNow.AddMinutes(1);}
                }
            } catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Falha ao retomar entradas persistidas do WhatsApp.");
                using var scope=scopes.CreateScope();
                await MKSANCrud.Services.Clinica.OperacaoService.RegistrarAsync(scope.ServiceProvider,"whatsapp-pendencias",false,ct); }
        } } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
