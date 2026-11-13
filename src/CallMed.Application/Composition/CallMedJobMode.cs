using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.Email;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Extensions;

public static class CallMedJobMode
{
    public static bool Embedded(IConfiguration configuration) => Mode(configuration) == "embedded";
    public static string Mode(IConfiguration configuration)
    {
        var mode = (configuration["Jobs:Mode"] ?? "embedded").Trim().ToLowerInvariant();
        return mode is "embedded" or "external" or "disabled" ? mode
            : throw new InvalidOperationException("Jobs:Mode deve ser embedded, external ou disabled.");
    }
    public static IServiceCollection AddCallMedJobs(this IServiceCollection services)
    {
        services.AddHostedService<PepEntregaBackgroundService>();
        services.AddHostedService<AgendaRenovacaoBackgroundService>();
        services.AddHostedService<ListaEsperaBackgroundService>();
        services.AddHostedService<LembreteConsultaBackgroundService>();
        services.AddHostedService<WhatsAppPendingBackgroundService>();
        services.AddHostedService<EphemeralCleanupBackgroundService>();
        services.AddHostedService<GmailInboxPollingService>();
        services.AddHostedService<AtendimentoFeedbackBackgroundService>();
        services.AddHostedService<AtendimentoEmailAutomaticoBackgroundService>();
        services.AddHostedService<HttpSmsAtendimentoBackgroundService>();
        return services;
    }
}
