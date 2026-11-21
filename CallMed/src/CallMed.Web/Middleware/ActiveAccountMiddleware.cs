using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Middleware;

public sealed class ActiveAccountMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, MKSANContext db, SignInManager<Usuario> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier); var email = context.User.Identity?.Name;
        var portal = PortaisAcesso.DoUsuario(context.User); var papel = portal.Papel;
        var sessao = ContaService.SessaoAtual(context.User);
        var claimSessao = context.User.FindFirstValue(ContaService.SessaoClaim);
        var stamp = context.User.FindFirstValue(signInManager.Options.ClaimsIdentity.SecurityStampClaimType);
        // A situação clínica, o stamp e o acesso são conferidos numa única consulta.
        // Isso permite revogação imediata sem acrescentar roundtrips ao chat/polling.
        var estado = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new
        {
            u.SecurityStamp,
            Ativo = papel == "Paciente" ? db.Pacientes.Any(p => p.Ativo && (p.UsuarioId == id || (p.UsuarioId == null && email != null && p.Email != null && p.Email.ToLower() == email.ToLower()))) :
                (papel == "Psicologo" || papel == "Medico") ? db.Medicos.Any(m => m.Ativo && (m.UsuarioId == id || (m.UsuarioId == null && email != null && m.Email != null && m.Email.ToLower() == email.ToLower()))) :
                papel == "Admin" ? (db.Funcionarios.Where(f => f.UsuarioId == id || (f.UsuarioId == null && email != null && f.Email.ToLower() == email.ToLower())).Select(f => (bool?)f.Ativo).FirstOrDefault() ?? true) :
                db.Funcionarios.Any(f => f.Ativo && (f.UsuarioId == id || (f.UsuarioId == null && email != null && f.Email.ToLower() == email.ToLower()))),
            SessaoValida = !sessao.HasValue || db.ContaSessoes.Any(s => s.Id == sessao && s.UsuarioId == id && s.RevogadaEm == null)
        }).FirstOrDefaultAsync(context.RequestAborted);
        var inativo = estado?.Ativo != true;
        var invalido = estado is null || (!string.IsNullOrEmpty(stamp) && stamp != estado.SecurityStamp) ||
            (claimSessao is not null && !sessao.HasValue) || estado?.SessaoValida != true;
        if (inativo || invalido)
        {
            await signInManager.SignOutAsync();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Redirect(context.Request.PathBase + portal.UrlLogin + (inativo ? "?contaInativa=true" : "?sessaoEncerrada=true"));
            return;
        }
        await next(context);
    }
}
