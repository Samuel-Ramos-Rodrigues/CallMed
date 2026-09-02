using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Middleware;

/// <summary>
/// Bloqueia imediatamente cookies de contas cujo cadastro clínico/administrativo
/// foi desativado. Lockout sozinho protege novos logins, mas não invalida uma
/// sessão que já estava aberta.
/// </summary>
public sealed class ActiveAccountMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveAccountMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        MKSANContext db,
        SignInManager<Usuario> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = context.User.Identity?.Name;
        var ativo = true;

        if (context.User.IsInRole("Paciente"))
        {
            ativo = await db.Pacientes.AsNoTracking().AnyAsync(p =>
                p.Ativo &&
                ((!string.IsNullOrWhiteSpace(userId) && p.UsuarioId == userId) ||
                 (p.UsuarioId == null && email != null && p.Email.ToLower() == email.ToLower())));
        }
        else if (context.User.IsInRole("Admin"))
        {
            // Admin pode existir apenas no Identity. Se houver cadastro administrativo
            // espelhado, respeita a situação ativa/inativa desse cadastro.
            var funcionarioAdmin = await db.Funcionarios.AsNoTracking().FirstOrDefaultAsync(f =>
                (!string.IsNullOrWhiteSpace(userId) && f.UsuarioId == userId) ||
                (f.UsuarioId == null && email != null && f.Email.ToLower() == email.ToLower()));

            ativo = funcionarioAdmin?.Ativo ?? true;
        }
        else if (context.User.IsInRole("Funcionario"))
        {
            ativo = await db.Funcionarios.AsNoTracking().AnyAsync(f =>
                f.Ativo &&
                ((!string.IsNullOrWhiteSpace(userId) && f.UsuarioId == userId) ||
                 (f.UsuarioId == null && email != null && f.Email.ToLower() == email.ToLower())));
        }
        else if (context.User.IsInRole("Medico"))
        {
            ativo = await db.Medicos.AsNoTracking().AnyAsync(m =>
                m.Ativo &&
                ((!string.IsNullOrWhiteSpace(userId) && m.UsuarioId == userId) ||
                 (m.UsuarioId == null && email != null && m.Email != null && m.Email.ToLower() == email.ToLower())));
        }

        if (!ativo)
        {
            await signInManager.SignOutAsync();
            context.Response.Redirect("/Identity/Account/Login?contaInativa=1");
            return;
        }

        await _next(context);
    }
}
