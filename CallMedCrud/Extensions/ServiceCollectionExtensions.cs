using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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

        services
            .AddDefaultIdentity<Usuario>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
            .AddEntityFrameworkStores<MKSANContext>();

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
        services.AddRazorPages();
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
        services.AddSecurityInfrastructure();

        return services;
    }

    private static IServiceCollection AddClinicaServices(this IServiceCollection services)
    {
        services.AddSingleton<IClinicaClock, ClinicaClock>();
        services.AddScoped<EspecialidadeService>();
        services.AddScoped<AgendaMedicoService>();
        services.AddScoped<ListaEsperaService>();
        services.AddScoped<ConfirmacoesService>();
        services.AddScoped<ConvenioService>();
        services.AddScoped<ConvenioElegibilidadeService>();
        services.AddScoped<AuditoriaService>();
        services.AddScoped<SolicitacaoAtendimentoService>();
        services.AddScoped<AgendamentoService>();
        services.AddScoped<UsuarioVinculoService>();

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
        services.Configure<EmailInboundOptions>(
            configuration.GetSection(EmailInboundOptions.SectionName));

        services.AddScoped<AtendimentoIdentidadeService>();
        services.AddScoped<AtendimentoConversaService>();
        services.AddScoped<AtendimentoEnvioService>();
        services.AddScoped<AtendimentoOrquestradorService>();

        services.AddHttpClient<EvolutionWhatsAppSender>();
        services.AddScoped<ICanalAtendimentoSender>(
            sp => sp.GetRequiredService<EvolutionWhatsAppSender>());

        services.AddHttpClient<SmsHttpSender>();
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
        return services;
    }

    private static IServiceCollection AddSecurityInfrastructure(this IServiceCollection services)
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

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
