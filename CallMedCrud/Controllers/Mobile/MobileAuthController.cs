using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Mobile;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers.Mobile;

[ApiController, Route("api/mobile/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(24576)]
public sealed class MobileAuthController(UserManager<Usuario> users, SignInManager<Usuario> signIn, PortalAcessoService acesso,
    UsuarioVinculoService vinculos, MKSANContext db, IOptionsMonitor<BearerTokenOptions> bearer, TimeProvider clock, MobileSessionService sessions) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("mobile-auth")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken ct)
    {
        var conta = await acesso.ObterContaUnificadaAsync(input.Usuario);
        if (conta.Usuario is null || conta.Portal?.EhPaciente != true) return Falha();
        var user = conta.Usuario;
        if ((await users.GetRolesAsync(user)).Any(r => r is "Admin" or "Psicologo" or "Medico" or "Funcionario")) return Falha();
        var result = await signIn.CheckPasswordSignInAsync(user, input.Senha, lockoutOnFailure: true);
        if (!result.Succeeded || await users.GetTwoFactorEnabledAsync(user)) return Falha();
        var principal = await signIn.CreateUserPrincipalAsync(user);
        if ((await vinculos.ObterPacienteAsync(principal, ct))?.Ativo != true) return Falha();
        var sessao = new ContaSessao { UsuarioId = user.Id, Dispositivo = "Aplicativo CallMed · Android" };
        db.ContaSessoes.Add(sessao); db.ContaEventos.Add(new() { UsuarioId = user.Id, Descricao = "Login pelo aplicativo Android" });
        await db.SaveChangesAsync(ct);
        MobileAuth.AdicionarSessao(principal, sessao.Id);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(MobileAuth.RefreshVersionClaim, sessao.RefreshVersion.ToString()));
        return SignIn(principal, MobileAuth.Scheme);
    }
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("mobile-auth")]
    public async Task<IActionResult> Refresh(RefreshInput input, CancellationToken ct)
    {
        var ticket = bearer.Get(MobileAuth.Scheme).RefreshTokenProtector.Unprotect(input.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || expires <= clock.GetUtcNow() || await sessions.ValidarAsync(ticket.Principal, ct) is null) return Falha();
        var user = await users.GetUserAsync(ticket.Principal); if (user is null) return Falha();
        var id = ContaService.SessaoAtual(ticket.Principal)!.Value;
        if (!Guid.TryParse(ticket.Principal.FindFirstValue(MobileAuth.RefreshVersionClaim), out var previous)) return Falha();
        var next = Guid.NewGuid(); var absoluteLimit = clock.GetUtcNow().UtcDateTime.AddDays(-14);
        if (db.Database.IsRelational())
        {
            var changed = await db.ContaSessoes.Where(s => s.Id == id && s.UsuarioId == user.Id && s.RevogadaEm == null &&
                s.CriadoEm > absoluteLimit && s.RefreshVersion == previous)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.RefreshVersion, next), ct);
            if (changed != 1)
            {
                await db.ContaSessoes.Where(s => s.Id == id && s.UsuarioId == user.Id && s.RevogadaEm == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevogadaEm, clock.GetUtcNow().UtcDateTime), ct);
                return Falha();
            }
        }
        else
        {
            var session = await db.ContaSessoes.SingleOrDefaultAsync(s => s.Id == id && s.UsuarioId == user.Id, ct);
            if (session is null || session.RevogadaEm != null || session.CriadoEm <= absoluteLimit) return Falha();
            if (session.RefreshVersion != previous) { session.RevogadaEm = clock.GetUtcNow().UtcDateTime; await db.SaveChangesAsync(ct); return Falha(); }
            session.RefreshVersion = next;
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Falha(); }
        }
        var principal = await signIn.CreateUserPrincipalAsync(user);
        MobileAuth.AdicionarSessao(principal, id);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(MobileAuth.RefreshVersionClaim, next.ToString()));
        return SignIn(principal, MobileAuth.Scheme);
    }
    [HttpPost("logout"), Authorize(Policy = MobileAuth.Policy)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var uid = users.GetUserId(User); var id = ContaService.SessaoAtual(User);
        var sessao = await db.ContaSessoes.FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == uid, ct);
        if (sessao is not null) { sessao.RevogadaEm = clock.GetUtcNow().UtcDateTime; db.ContaEventos.Add(new() { UsuarioId = uid!, Descricao = "Saída do aplicativo Android" }); await db.SaveChangesAsync(ct); }
        return Ok(new { mensagem = "Acesso encerrado." });
    }
    private UnauthorizedObjectResult Falha() => Unauthorized(new { mensagem = "CPF/e-mail ou senha inválidos, ou acesso indisponível. Confira sua conta de paciente." });
    public sealed class LoginInput
    {
        [Required, StringLength(256)] public string Usuario { get; set; } = "";
        [Required, StringLength(256)] public string Senha { get; set; } = "";
    }
    public sealed class RefreshInput
    {
        [Required, StringLength(16384)] public string RefreshToken { get; set; } = "";
    }
}
