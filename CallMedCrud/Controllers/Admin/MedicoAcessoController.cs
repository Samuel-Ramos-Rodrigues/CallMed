using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public class MedicoAcessoController : Controller
{
    private readonly MKSANContext _context;
    private readonly UserManager<Usuario> _userManager;

    public MedicoAcessoController(MKSANContext context, UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Gerenciar(int id)
    {
        var medico = await _context.Medicos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (medico is null) return NotFound();

        return View(new MedicoAcessoViewModel
        {
            MedicoId = medico.Id,
            MedicoNome = medico.Nome,
            Email = medico.Email ?? string.Empty,
            PossuiAcesso = !string.IsNullOrWhiteSpace(medico.UsuarioId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Gerenciar(MedicoAcessoViewModel model)
    {
        var medico = await _context.Medicos.FirstOrDefaultAsync(m => m.Id == model.MedicoId);
        if (medico is null) return NotFound();

        model.MedicoNome = medico.Nome;
        model.PossuiAcesso = !string.IsNullOrWhiteSpace(medico.UsuarioId);

        var email = model.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
            ModelState.AddModelError(nameof(model.Email), "Informe o e-mail do médico.");

        var usuarioExistentePorEmail = string.IsNullOrWhiteSpace(email)
            ? null
            : await _userManager.FindByEmailAsync(email);

        if (string.IsNullOrWhiteSpace(medico.UsuarioId) && usuarioExistentePorEmail is null && string.IsNullOrWhiteSpace(model.Senha))
            ModelState.AddModelError(nameof(model.Senha), "Informe uma senha inicial para criar o acesso.");

        if (!ModelState.IsValid) return View(model);

        if (await _context.Medicos.AnyAsync(m => m.Id != medico.Id && m.Email != null && m.Email.ToLower() == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Esse e-mail já está vinculado a outro médico.");
            return View(model);
        }

        if (await _context.Pacientes.AnyAsync(p => p.Email.ToLower() == email) ||
            await _context.Funcionarios.AnyAsync(f => f.Email.ToLower() == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Esse e-mail já pertence a um paciente ou funcionário.");
            return View(model);
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        Usuario? usuario;

        if (!string.IsNullOrWhiteSpace(medico.UsuarioId))
        {
            usuario = await _userManager.FindByIdAsync(medico.UsuarioId);
            if (usuario is null)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "O vínculo de acesso está inconsistente. Remova o vínculo no banco ou contate o administrador.");
                return View(model);
            }

            var outro = await _userManager.FindByEmailAsync(email);
            if (outro is not null && outro.Id != usuario.Id)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(nameof(model.Email), "Esse e-mail já pertence a outra conta.");
                return View(model);
            }

            if (!string.Equals(usuario.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                var emailResult = await _userManager.SetEmailAsync(usuario, email);
                if (!emailResult.Succeeded)
                {
                    await tx.RollbackAsync();
                    AdicionarErros(emailResult);
                    return View(model);
                }

                var usernameResult = await _userManager.SetUserNameAsync(usuario, email);
                if (!usernameResult.Succeeded)
                {
                    await tx.RollbackAsync();
                    AdicionarErros(usernameResult);
                    return View(model);
                }
            }
        }
        else
        {
            usuario = usuarioExistentePorEmail;
            if (usuario is null)
            {
                usuario = new Usuario { UserName = email, Email = email, EmailConfirmed = true };
                var criado = await _userManager.CreateAsync(usuario, model.Senha!);
                if (!criado.Succeeded)
                {
                    await tx.RollbackAsync();
                    AdicionarErros(criado);
                    return View(model);
                }
            }
            else
            {
                var vinculado = await _context.Medicos.AnyAsync(m => m.Id != medico.Id && m.UsuarioId == usuario.Id);
                if (vinculado ||
                    await _userManager.IsInRoleAsync(usuario, "Paciente") ||
                    await _userManager.IsInRoleAsync(usuario, "Funcionario") ||
                    await _userManager.IsInRoleAsync(usuario, "Admin"))
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(nameof(model.Email), "Esse e-mail já pertence a outro perfil do sistema.");
                    return View(model);
                }
            }
        }

        if (!await _userManager.IsInRoleAsync(usuario, "Medico"))
        {
            var role = await _userManager.AddToRoleAsync(usuario, "Medico");
            if (!role.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(role);
                return View(model);
            }
        }

        medico.UsuarioId = usuario.Id;
        medico.Email = email;

        try
        {
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Não foi possível concluir o vínculo do acesso médico. Verifique se o e-mail já está em uso.");
            return View(model);
        }

        TempData["Sucesso"] = "Acesso do médico configurado.";
        return RedirectToAction("Details", "Medico", new { id = medico.Id });
    }

    private void AdicionarErros(IdentityResult result)
    {
        foreach (var erro in result.Errors)
            ModelState.AddModelError(string.Empty, erro.Description);
    }
}
