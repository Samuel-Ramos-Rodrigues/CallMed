using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Paciente")]
public class MinhaContaController : Controller
{
    private readonly MKSANContext _context;
    private readonly UserManager<Usuario> _userManager;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly UsuarioVinculoService _vinculos;
    private readonly IClinicaClock _clock;

    public MinhaContaController(
        MKSANContext context,
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager,
        UsuarioVinculoService vinculos,
        IClinicaClock clock)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _vinculos = vinculos;
        _clock = clock;
    }

    public async Task<IActionResult> Index()
    {
        var paciente = await _vinculos.ObterPacienteAsync(User);
        return paciente is null ? NotFound() : View(ParaViewModel(paciente));
    }

    public async Task<IActionResult> Edit()
    {
        var paciente = await _vinculos.ObterPacienteAsync(User);
        return paciente is null ? NotFound() : View(ParaViewModel(paciente));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MinhaContaViewModel model)
    {
        var atual = await _vinculos.ObterPacienteAsync(User);
        if (atual is null || atual.Id != model.Id || !atual.Ativo)
            return Forbid();

        model.Cpf = atual.Cpf;
        model.CriadoEm = atual.CriadoEm;
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.Nome = model.Nome?.Trim() ?? string.Empty;
        model.Telefone = model.Telefone?.Trim();
        model.DataNascimento = model.DataNascimento?.Date;

        if (!CadastroValidator.DataNascimentoValida(model.DataNascimento, _clock.Hoje))
            ModelState.AddModelError(nameof(model.DataNascimento), "Informe uma data de nascimento válida.");

        if (model.TemConvenio)
        {
            if (string.IsNullOrWhiteSpace(model.NomeConvenio))
                ModelState.AddModelError(nameof(model.NomeConvenio), "Informe o nome do convênio.");

            if (model.ValidadeConvenio.HasValue && model.ValidadeConvenio.Value.Date < _clock.Hoje)
                ModelState.AddModelError(nameof(model.ValidadeConvenio), "A validade do convênio está vencida.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        if (await _context.Pacientes.AnyAsync(p =>
                p.Id != atual.Id &&
                p.Email.ToLower() == model.Email.ToLower()))
        {
            ModelState.AddModelError(nameof(model.Email), "E-mail já cadastrado.");
            return View(model);
        }

        var identityComEmail = await _userManager.FindByEmailAsync(model.Email);
        if (identityComEmail is not null && identityComEmail.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "Esse e-mail já possui uma conta cadastrada.");
            return View(model);
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        if (!string.Equals(atual.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, model.Email);
            if (!emailResult.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(nameof(model.Email), emailResult);
                return View(model);
            }

            var userNameResult = await _userManager.SetUserNameAsync(user, model.Email);
            if (!userNameResult.Succeeded)
            {
                await tx.RollbackAsync();
                AdicionarErros(nameof(model.Email), userNameResult);
                return View(model);
            }
        }

        atual.UsuarioId = user.Id;
        atual.Nome = model.Nome;
        atual.Email = model.Email;
        atual.Telefone = model.Telefone;
        atual.DataNascimento = model.DataNascimento;
        atual.TemConvenio = model.TemConvenio;
        atual.NomeConvenio = model.TemConvenio ? model.NomeConvenio?.Trim() : null;
        atual.NumeroConvenio = model.TemConvenio ? model.NumeroConvenio?.Trim() : null;
        atual.ValidadeConvenio = model.TemConvenio ? model.ValidadeConvenio?.Date : null;
        atual.CanalPreferido = NormalizarCanalPreferido(model.CanalPreferido);

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        await _signInManager.RefreshSignInAsync(user);

        TempData["Sucesso"] = "Dados atualizados com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete()
    {
        var paciente = await _vinculos.ObterPacienteAsync(User);
        var user = await _userManager.GetUserAsync(User);

        if (paciente is null || user is null)
            return NotFound();

        // Excluir a conta remove o acesso, mas preserva o prontuário administrativo
        // e o histórico de consultas do paciente.
        await using var tx = await _context.Database.BeginTransactionAsync();

        paciente.Ativo = false;
        paciente.UsuarioId = null;
        await _context.SaveChangesAsync();

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            await tx.RollbackAsync();
            TempData["Erro"] = "Não foi possível excluir o acesso agora. Tente novamente.";
            return RedirectToAction(nameof(Index));
        }

        await tx.CommitAsync();
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private void AdicionarErros(string campo, IdentityResult result)
    {
        foreach (var erro in result.Errors)
            ModelState.AddModelError(campo, erro.Description);
    }

    private static MinhaContaViewModel ParaViewModel(Paciente paciente) => new()
    {
        Id = paciente.Id,
        Nome = paciente.Nome,
        Cpf = paciente.Cpf,
        Email = paciente.Email,
        Telefone = paciente.Telefone,
        DataNascimento = paciente.DataNascimento,
        TemConvenio = paciente.TemConvenio,
        NomeConvenio = paciente.NomeConvenio,
        NumeroConvenio = paciente.NumeroConvenio,
        ValidadeConvenio = paciente.ValidadeConvenio,
        CanalPreferido = paciente.CanalPreferido,
        CriadoEm = paciente.CriadoEm
    };

    private static string NormalizarCanalPreferido(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "sms" => "SMS",
        "email" or "e-mail" => "Email",
        "telefone" => "Telefone",
        _ => "WhatsApp"
    };
}
