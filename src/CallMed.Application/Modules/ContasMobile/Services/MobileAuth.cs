using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Services.Mobile;

public static class MobileAuth
{
    public const string Scheme = "CallMedMobile";
    public const string Policy = "CallMedPacienteMobile";
    public const string RefreshVersionClaim = "callmed:refresh-version";
    public const string ScopeClaim = "callmed:mobile";
    public const string PacienteItem = "CallMedMobilePacienteId";
    public static void AdicionarSessao(ClaimsPrincipal principal, Guid sessao)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new(ContaService.SessaoClaim, sessao.ToString()));
        identity.AddClaim(new(ScopeClaim, "paciente"));
    }
    public static IServiceCollection AddCallMedMobile(this IServiceCollection services)
    {
        // Mantém o cookie do site como padrão. Estes tokens só autenticam a API móvel.
        services.AddAuthentication().AddBearerToken(Scheme, options =>
        {
            options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
            options.RefreshTokenExpiration = TimeSpan.FromDays(14);
        });
        services.AddAuthorization(options => options.AddPolicy(Policy, policy =>
        {
            policy.AuthenticationSchemes.Add(Scheme);
            policy.RequireAuthenticatedUser().RequireRole("Paciente").AddRequirements(new MobilePacienteRequirement());
        }));
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, MobileAuthorizationResultHandler>();
        services.AddScoped<MobileSessionService>();
        services.AddScoped<IAuthorizationHandler, MobilePacienteHandler>();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("mobile-auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonimo", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 30, Window = TimeSpan.FromMinutes(5), QueueLimit = 0, AutoReplenishment = true }));
        });
        return services;
    }
}
public sealed class MobilePacienteRequirement : IAuthorizationRequirement { }
public sealed class MobileSessionService(MKSANContext db, IOptions<IdentityOptions> identity)
{
    public async Task<int?> ValidarAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var uid = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessao = ContaService.SessaoAtual(principal);
        var stamp = principal.FindFirstValue(identity.Value.ClaimsIdentity.SecurityStampClaimType);
        if (string.IsNullOrWhiteSpace(uid) || !sessao.HasValue || string.IsNullOrEmpty(stamp) || principal.FindFirstValue(MobileAuth.ScopeClaim) != "paciente") return null;
        // A identidade, o papel atual, a sessão e o vínculo clínico são verificados juntos.
        return await db.Pacientes.AsNoTracking().Where(p => p.Ativo && p.UsuarioId == uid &&
            db.Users.Any(u => u.Id == uid && u.SecurityStamp == stamp) &&
            db.UserRoles.Any(ur => ur.UserId == uid && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Paciente")) &&
            !db.UserRoles.Any(ur => ur.UserId == uid && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "Admin" || r.Name == "Psicologo" || r.Name == "Medico" || r.Name == "Funcionario"))) &&
            db.ContaSessoes.Any(s => s.Id == sessao && s.UsuarioId == uid && s.RevogadaEm == null))
            .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
    }
}
public sealed class MobilePacienteHandler(MobileSessionService sessions, IHttpContextAccessor accessor) : AuthorizationHandler<MobilePacienteRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MobilePacienteRequirement requirement)
    {
        var http = accessor.HttpContext;
        if (http is null) return;
        var id = await sessions.ValidarAsync(context.User, http.RequestAborted);
        if (id.HasValue) { http.Items[MobileAuth.PacienteItem] = id.Value; context.Succeed(requirement); }
    }
}

// Invalid/revoked mobile credentials produce 401. A controller may still return
// 403 for a valid session attempting a business operation it cannot perform.
public sealed class MobileAuthorizationResultHandler : Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler
{
    private readonly Microsoft.AspNetCore.Authorization.Policy.AuthorizationMiddlewareResultHandler fallback = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, Microsoft.AspNetCore.Authorization.Policy.PolicyAuthorizationResult result)
    {
        if (result.Forbidden && policy.Requirements.Any(r => r is MobilePacienteRequirement))
        { await context.ChallengeAsync(MobileAuth.Scheme); return; }
        await fallback.HandleAsync(next, context, policy, result);
    }
}
