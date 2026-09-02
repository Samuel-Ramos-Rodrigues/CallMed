using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public class FuncionarioController : Controller
{
    private static readonly HashSet<string> CargosPermitidos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Atendente",
            "Recepcionista",
            "Administrador"
        };

    private readonly MKSANContext _context;
    private readonly UserManager<Usuario> _userManager;

    public FuncionarioController(
        MKSANContext context,
        UserManager<Usuario> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index() =>
        View(await _context.Funcionarios.AsNoTracking().OrderBy(f => f.Nome).ToListAsync());

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Funcionarios.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        return item is null ? NotFound() : View(item);
    }

    public IActionResult Create() => View(new Funcionario { Ativo = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Id,Nome,Email,Senha,Cargo,Ativo")] Funcionario funcionario)
    {
        Normalizar(funcionario);

        if (string.IsNullOrWhiteSpace(funcionario.Senha))
            ModelState.AddModelError(nameof(Funcionario.Senha), "Informe uma senha inicial.");

        if (!CargosPermitidos.Contains(funcionario.Cargo))
            ModelState.AddModelError(nameof(Funcionario.Cargo), "Cargo inválido.");

        if (await _context.Funcionarios.AnyAsync(f => f.Email.ToLower() == funcionario.Email.ToLower()))
            ModelState.AddModelError(nameof(Funcionario.Email), "E-mail já cadastrado.");
        if (await _context.Medicos.AnyAsync(m => m.Email != null && m.Email.ToLower() == funcionario.Email.ToLower()) ||
            await _context.Pacientes.AnyAsync(p => p.Email.ToLower() == funcionario.Email.ToLower()))
            ModelState.AddModelError(nameof(Funcionario.Email), "Esse e-mail já pertence a um médico ou paciente.");

        if (await _userManager.FindByEmailAsync(funcionario.Email) is not null)
            ModelState.AddModelError(nameof(Funcionario.Email), "Esse e-mail já possui uma conta.");

        if (!ModelState.IsValid)
            return View(funcionario);

        await using var tx = await _context.Database.BeginTransactionAsync();

        var user = new Usuario
        {
            UserName = funcionario.Email,
            Email = funcionario.Email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, funcionario.Senha!);
        if (!createResult.Succeeded)
        {
            await tx.RollbackAsync();
            AdicionarErros(string.Empty, createResult);
            return View(funcionario);
        }

        var role = EhAdministrador(funcionario.Cargo) ? "Admin" : "Funcionario";
        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await tx.RollbackAsync();
            AdicionarErros(string.Empty, roleResult);
            return View(funcionario);
        }

        funcionario.UsuarioId = user.Id;
        funcionario.CriadoEm = DateTime.UtcNow;
        _context.Funcionarios.Add(funcionario);

        try
        {
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Sucesso"] = "Funcionário cadastrado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Não foi possível concluir o cadastro do funcionário.");
            return View(funcionario);
        }
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Funcionarios.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,Nome,Email,Cargo,Ativo")] Funcionario model)
    {
        if (id != model.Id)
            return NotFound();

        Normalizar(model);

        if (!CargosPermitidos.Contains(model.Cargo))
            ModelState.AddModelError(nameof(Funcionario.Cargo), "Cargo inválido.");

        var atual = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Id == id);
        if (atual is null)
            return NotFound();

        var estavaAtivo = atual.Ativo;

        if (await _context.Funcionarios.AnyAsync(f =>
                f.Id != id &&
                f.Email.ToLower() == model.Email.ToLower()))
        {
            ModelState.AddModelError(nameof(Funcionario.Email), "E-mail já cadastrado.");
        }

        if (await _context.Medicos.AnyAsync(m => m.Email != null && m.Email.ToLower() == model.Email.ToLower()) ||
            await _context.Pacientes.AnyAsync(p => p.Email.ToLower() == model.Email.ToLower()))
        {
            ModelState.AddModelError(nameof(Funcionario.Email), "Esse e-mail já pertence a um médico ou paciente.");
        }

        var user = !string.IsNullOrWhiteSpace(atual.UsuarioId)
            ? await _userManager.FindByIdAsync(atual.UsuarioId)
            : await _userManager.FindByEmailAsync(atual.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "A conta de acesso desse funcionário não foi encontrada.");
            return View(model);
        }

        var eraAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        var continuaraAdmin = EhAdministrador(model.Cargo) && model.Ativo;

        if (eraAdmin && !continuaraAdmin && !await ExisteOutroAdminAtivoAsync(user.Id))
        {
            ModelState.AddModelError(
                string.Empty,
                "Não é possível remover ou desativar o último administrador ativo do sistema.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var identityComEmail = await _userManager.FindByEmailAsync(model.Email);
        if (identityComEmail is not null && identityComEmail.Id != user.Id)
        {
            ModelState.AddModelError(nameof(Funcionario.Email), "Esse e-mail já está sendo utilizado por outra conta.");
            return View(model);
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        if (!string.Equals(atual.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, model.Email);
            if (!emailResult.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(nameof(Funcionario.Email), emailResult);
                return View(model);
            }

            var userNameResult = await _userManager.SetUserNameAsync(user, model.Email);
            if (!userNameResult.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(nameof(Funcionario.Email), userNameResult);
                return View(model);
            }
        }

        var rolesAtuais = await _userManager.GetRolesAsync(user);
        var roleDesejada = EhAdministrador(model.Cargo) ? "Admin" : "Funcionario";
        var roleAlterada = false;

        if (!rolesAtuais.Contains(roleDesejada, StringComparer.OrdinalIgnoreCase))
        {
            if (rolesAtuais.Count > 0)
            {
                var remover = await _userManager.RemoveFromRolesAsync(user, rolesAtuais);
                if (!remover.Succeeded)
                {
                    await tx.RollbackAsync();
                    AdicionarErros(string.Empty, remover);
                    return View(model);
                }
            }

            var adicionar = await _userManager.AddToRoleAsync(user, roleDesejada);
            if (!adicionar.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(string.Empty, adicionar);
                return View(model);
            }

            roleAlterada = true;
        }

        if (model.Ativo && !estavaAtivo)
        {
            var desbloquear = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!desbloquear.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(string.Empty, desbloquear);
                return View(model);
            }

            await _userManager.ResetAccessFailedCountAsync(user);
        }
        else if (!model.Ativo && estavaAtivo)
        {
            var habilitarLockout = await _userManager.SetLockoutEnabledAsync(user, true);
            var bloquear = habilitarLockout.Succeeded
                ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)
                : habilitarLockout;

            if (!bloquear.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(string.Empty, bloquear);
                return View(model);
            }
        }

        if (roleAlterada || model.Ativo != estavaAtivo)
        {
            var stamp = await _userManager.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(string.Empty, stamp);
                return View(model);
            }
        }

        atual.UsuarioId = user.Id;
        atual.Nome = model.Nome;
        atual.Email = model.Email;
        atual.Cargo = model.Cargo;
        atual.Ativo = model.Ativo;

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Sucesso"] = "Funcionário atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
            return NotFound();

        var item = await _context.Funcionarios.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Id == id);
        if (item is null)
            return RedirectToAction(nameof(Index));

        var user = !string.IsNullOrWhiteSpace(item.UsuarioId)
            ? await _userManager.FindByIdAsync(item.UsuarioId)
            : await _userManager.FindByEmailAsync(item.Email);

        if (user is not null)
        {
            var atualUserId = _userManager.GetUserId(User);
            if (user.Id == atualUserId)
            {
                TempData["Erro"] = "Você não pode desativar a própria conta por esta tela.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Admin") &&
                !await ExisteOutroAdminAtivoAsync(user.Id))
            {
                TempData["Erro"] = "Não é possível desativar o último administrador ativo.";
                return RedirectToAction(nameof(Index));
            }
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        if (user is not null)
        {
            var habilitarLockout = await _userManager.SetLockoutEnabledAsync(user, true);
            var bloquear = habilitarLockout.Succeeded
                ? await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)
                : habilitarLockout;

            if (!bloquear.Succeeded)
            {
                await tx.RollbackAsync();
                TempData["Erro"] = "Não foi possível bloquear a conta do funcionário.";
                return RedirectToAction(nameof(Index));
            }

            var stamp = await _userManager.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded)
            {
                await tx.RollbackAsync();
                TempData["Erro"] = "Não foi possível invalidar as sessões do funcionário.";
                return RedirectToAction(nameof(Index));
            }
        }

        item.Ativo = false;
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["Sucesso"] = "Funcionário desativado. O cadastro foi preservado para auditoria.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ExisteOutroAdminAtivoAsync(string ignorarUserId)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");

        foreach (var admin in admins.Where(a => a.Id != ignorarUserId))
        {
            var funcionario = await _context.Funcionarios
                .AsNoTracking()
                .FirstOrDefaultAsync(f =>
                    f.UsuarioId == admin.Id ||
                    (admin.Email != null && f.Email.ToLower() == admin.Email.ToLower()));

            // Conta Admin sem registro de funcionário é considerada ativa por segurança.
            if (funcionario is null || funcionario.Ativo)
                return true;
        }

        return false;
    }

    private static bool EhAdministrador(string? cargo) =>
        string.Equals(cargo, "Administrador", StringComparison.OrdinalIgnoreCase);

    private static void Normalizar(Funcionario funcionario)
    {
        funcionario.Nome = funcionario.Nome?.Trim() ?? string.Empty;
        funcionario.Email = funcionario.Email?.Trim() ?? string.Empty;
        funcionario.Cargo = funcionario.Cargo?.Trim() ?? "Atendente";
    }

    private void AdicionarErros(string campo, IdentityResult result)
    {
        foreach (var erro in result.Errors)
            ModelState.AddModelError(campo, erro.Description);
    }
}
