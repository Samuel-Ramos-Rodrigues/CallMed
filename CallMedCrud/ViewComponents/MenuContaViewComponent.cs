using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

namespace MKSANCrud.ViewComponents;

public sealed class MenuContaViewComponent(MKSANContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var principal = HttpContext.User;
        var portal = PortaisAcesso.DoUsuario(principal);
        var fallback = new MenuContaViewModel { Nome = portal.Nome, Papel = portal.Nome };
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) ||
            principal.HasClaim(c => c.Type == "clinical.consulta"))
            return View(fallback);

        // A single read of this account's metadata. Photo bytes and session history
        // are not loaded by the layout; the protected photo endpoint serves the image.
        var paciente = portal.EhPaciente;
        var profissional = portal.Papel is "Psicologo" or "Medico";
        var dados = await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new
            {
                Nome = (paciente ? db.Pacientes.Where(p => p.UsuarioId == id && p.Ativo)
                    .Select(p => p.Nome).FirstOrDefault() : null)
                    ?? (profissional ? db.Medicos.Where(m => m.UsuarioId == id && m.Ativo)
                        .Select(m => m.Nome).FirstOrDefault() : null)
                    ?? (!paciente && !profissional ? db.Funcionarios.Where(f => f.UsuarioId == id && f.Ativo)
                        .Select(f => f.Nome).FirstOrDefault() : null)
                    ?? db.ContaPerfis.Where(p => p.UsuarioId == id).Select(p => p.NomeExibicao).FirstOrDefault()
                    ?? u.UserName ?? portal.Nome,
                Email = u.Email ?? "",
                FotoVersao = db.ContaFotos.Where(f => f.UsuarioId == id)
                    .Select(f => (Guid?)f.Versao).FirstOrDefault()
            }).SingleOrDefaultAsync(HttpContext.RequestAborted);
        return View(dados is null ? fallback : new MenuContaViewModel
        {
            Nome = string.IsNullOrWhiteSpace(dados.Nome) ? portal.Nome : dados.Nome, Papel = portal.Nome, Email = dados.Email, FotoVersao = dados.FotoVersao
        });
    }
}
