using System.Threading.RateLimiting;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Options;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.Email;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Database;
using MKSANCrud.Services.Email;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.Services.Mobile;

namespace MKSANCrud.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCallMedApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("MKSANContextConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'MKSANContextConnection' not found.");

        services.AddDbContext<MKSANContext>(options =>
            options.UseNpgsql(connectionString));

        // O Render pode substituir o contêiner. As chaves usadas pelos links e cookies
        // precisam sobreviver ao processo e ser compartilhadas entre instâncias.
        services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();

        services
            .AddDefaultIdentity<Usuario>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
            .AddEntityFrameworkStores<MKSANContext>()
            .AddPasswordValidator<SpecialCharacterPasswordValidator>();

        services.AddCallMedMobile();

        services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = context =>
        {
            // A renovação periódica do Identity deve preservar o identificador do acesso.
            var sessao = context.CurrentPrincipal?.FindFirst(ContaService.SessaoClaim);
            if (sessao is not null && context.NewPrincipal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                identity.AddClaim(sessao);
            return Task.CompletedTask;
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        services.AddControllersWithViews();
        services.AddRazorPages(options =>
            options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "acesso/{perfil=paciente}"));
        services.AddHttpContextAccessor();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(1);
        });

        services.AddClinicaServices();
        services.AddAtendimentoServices(configuration);
        services.AddAgenteServices(configuration);
        services.AddEmailServices(configuration);
        services.AddDatabaseInitializers();
        services.AddSecurityInfrastructure(configuration);

        return services;
    }

    private static IServiceCollection AddClinicaServices(this IServiceCollection services)
    {
        services.AddSingleton<IClinicaClock, ClinicaClock>();
        services.AddScoped<EspecialidadeService>();
        services.AddScoped<AgendaMedicoService>();
        services.AddScoped<ListaEsperaService>();
        services.AddScoped<ConfirmacoesService>();
        services.AddSingleton<AcaoConsultaLinkService>();
        services.AddSingleton<TelemedicinaService>();
        services.AddScoped<ConvenioService>();
        services.AddScoped<ConvenioElegibilidadeService>();
        services.AddScoped<AuditoriaService>();
        services.AddScoped<SolicitacaoAtendimentoService>();
        services.AddScoped<AgendamentoService>();
        services.AddScoped<OrientacoesConsultaService>();
        services.AddScoped<UsuarioVinculoService>();
        services.AddScoped<ContaService>();
        services.AddScoped<PortalAcessoService>();

        services.AddHostedService<AgendaRenovacaoBackgroundService>();
        services.AddHostedService<ListaEsperaBackgroundService>();
        services.AddHostedService<LembreteConsultaBackgroundService>();

        return services;
    }

    private static IServiceCollection AddAtendimentoServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EvolutionWhatsAppOptions>(
            configuration.GetSection(EvolutionWhatsAppOptions.SectionName));
        services.Configure<SmsHttpOptions>(
            configuration.GetSection(SmsHttpOptions.SectionName));
        services.Configure<GmailInboxOptions>(
            configuration.GetSection(GmailInboxOptions.SectionName));
        services.Configure<AtendimentoEmailOptions>(configuration.GetSection(AtendimentoEmailOptions.SectionName));
        services.PostConfigure<AtendimentoEmailOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.PortalBaseUrl))
                options.PortalBaseUrl = configuration["RENDER_EXTERNAL_URL"] ?? configuration["Twa:Origin"] ?? string.Empty;
        });
        services.AddHttpClient("GmailApi");
        services.AddSingleton<GmailApiClient>();
        services.AddHostedService<GmailInboxPollingService>();
        services.AddScoped<AtendimentoIdentidadeService>();
        services.AddScoped<AtendimentoConversaService>();
        services.AddScoped<AtendimentoEnvioService>();
        services.AddScoped<AtendimentoFeedbackService>();
        services.AddHostedService<AtendimentoFeedbackBackgroundService>();
        services.AddScoped<AtendimentoOrquestradorService>();
        services.AddScoped<AtendimentoEmailDecisaoService>();
        services.AddScoped<AtendimentoEmailAutomaticoService>();
        services.AddHostedService<AtendimentoEmailAutomaticoBackgroundService>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<EvolutionEnvioControle>();
        services.AddHttpClient<EvolutionWhatsAppSender>();
        services.AddScoped<ICanalAtendimentoSender>(
            sp => sp.GetRequiredService<EvolutionWhatsAppSender>());

        services.AddHttpClient<SmsHttpSender>();
        services.AddScoped<HttpSmsAtendimentoService>();
        services.AddHostedService<HttpSmsAtendimentoBackgroundService>();
        services.AddScoped<ICanalAtendimentoSender>(
            sp => sp.GetRequiredService<SmsHttpSender>());

        services.AddScoped<SmtpAtendimentoSender>();
        services.AddScoped<ICanalAtendimentoSender>(
            sp => sp.GetRequiredService<SmtpAtendimentoSender>());

        return services;
    }

    private static IServiceCollection AddAgenteServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(
            configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<GeminiClient>();
        services.AddScoped<AgenteToolsService>();
        services.AddScoped<AgenteHistoricoService>();
        services.AddScoped<IAgenteClinicaService, AgenteClinicaService>();
        return services;
    }

    private static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));

        services.AddScoped<IEmailService, SmtpEmailService>();
        return services;
    }

    private static IServiceCollection AddDatabaseInitializers(this IServiceCollection services)
    {
        services.AddScoped<DatabaseSchemaV12Initializer>();
        services.AddScoped<DatabaseSchemaV13Initializer>();
        services.AddScoped<DatabaseSchemaV14Initializer>();
        services.AddScoped<DatabaseSchemaV15Initializer>();
        services.AddScoped<DatabaseSchemaV16Initializer>();
        services.AddScoped<DatabaseSchemaV21Initializer>();
        services.AddScoped<DatabaseSchemaV22Initializer>();
        services.AddScoped<DatabaseSchemaV23Initializer>();
        services.AddScoped<DatabaseSchemaV24Initializer>();
        services.AddScoped<DatabaseSchemaV25Initializer>();
        services.AddScoped<DatabaseSchemaV26Initializer>();
        services.AddScoped<DatabaseSchemaV27Initializer>();
        services.AddScoped<DatabaseSchemaV28Initializer>();
        services.AddScoped<DatabaseSchemaV29Initializer>();
        services.AddScoped<DatabaseSchemaV30Initializer>();
        services.AddScoped<DatabaseSchemaV31Initializer>();
        services.AddScoped<DatabaseSchemaV32Initializer>();
        return services;
    }

    private static IServiceCollection AddSecurityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("agente", httpContext =>
            {
                var chave = httpContext.User.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonimo";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: chave,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("email-test", httpContext =>
            {
                var chave = httpContext.User.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonimo";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: chave,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("webhooks", httpContext =>
            {
                var chave = httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "webhook";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: chave,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });
        });

        var trustedProxyAddresses = configuration
            .GetSection("ReverseProxy:KnownProxies")
            .Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;

            // Mantém os proxies/redes de loopback confiáveis padrão do ASP.NET.
            // Somente endereços explicitamente configurados passam a ser confiáveis;
            // nunca aceitar X-Forwarded-For de qualquer origem, pois isso permite
            // falsificar IP e contornar limites por endereço.
            foreach (var configuredAddress in trustedProxyAddresses)
            {
                if (string.IsNullOrWhiteSpace(configuredAddress))
                    continue;

                if (!IPAddress.TryParse(configuredAddress.Trim(), out var address))
                {
                    throw new InvalidOperationException(
                        $"Endereço inválido em ReverseProxy:KnownProxies: '{configuredAddress}'. Informe um IP, não um nome DNS.");
                }

                if (!options.KnownProxies.Contains(address))
                    options.KnownProxies.Add(address);
            }
        });

        return services;
    }
}
