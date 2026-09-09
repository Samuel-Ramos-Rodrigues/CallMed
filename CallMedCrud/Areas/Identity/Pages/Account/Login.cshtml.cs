using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<Usuario> _signInManager;
    private readonly UserManager<Usuario> _userManager;
    private readonly MKSANContext _context;

    public LoginModel(
        SignInManager<Usuario> signInManager,
        UserManager<Usuario> userManager,
        MKSANContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; set; } = "/";

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu CPF ou e-mail.")]
        [Display(Name = "CPF ou e-mail")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe sua senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Lembrar acesso")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var identificador = Input.Usuario.Trim();
        string? email = null;

        if (identificador.Contains('@'))
        {
            email = identificador;
        }
        else
        {
            var cpf = new string(identificador.Where(char.IsDigit).ToArray());
            if (cpf.Length == 11)
            {
                email = await _context.Pacientes
                    .AsNoTracking()
                    .Where(p => p.Cpf == cpf && p.Ativo)
                    .Select(p => p.Email)
                    .FirstOrDefaultAsync();
            }
        }

        if (string.IsNullOrWhiteSpace(email))
            return LoginInvalido();

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return LoginInvalido();

        var ehAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        var ehFuncionario = await _userManager.IsInRoleAsync(user, "Funcionario");
        var ehPaciente = await _userManager.IsInRoleAsync(user, "Paciente");
        var ehMedico = await _userManager.IsInRoleAsync(user, "Medico");

        if (ehAdmin || ehFuncionario)
        {
            var funcionario = await _context.Funcionarios
                .AsNoTracking()
                .FirstOrDefaultAsync(f =>
                    f.UsuarioId == user.Id ||
                    f.Email.ToLower() == email.ToLower());

            if (funcionario is null || !funcionario.Ativo)
                return LoginInvalido();
        }
        else if (ehMedico)
        {
            var medico = await _context.Medicos
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UsuarioId == user.Id || (m.Email != null && m.Email.ToLower() == email.ToLower()));
            if (medico is null || !medico.Ativo) return LoginInvalido();
        }
        else if (ehPaciente)
        {
            var paciente = await _context.Pacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.UsuarioId == user.Id ||
                    p.Email.ToLower() == email.ToLower());

            if (paciente is null || !paciente.Ativo)
                return LoginInvalido();
        }
        else
        {
            // Uma conta autenticável sem perfil conhecido não recebe acesso ao sistema.
            return LoginInvalido();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (ehAdmin || ehFuncionario)
                return RedirectToAction("Index", "FuncionarioPainel", new { area = "" });
            if (ehMedico)
                return RedirectToAction("Index", "MedicoPainel", new { area = "" });

            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "Muitas tentativas incorretas. Aguarde alguns minutos e tente novamente.");
            return Page();
        }

        return LoginInvalido();
    }

    private IActionResult LoginInvalido()
    {
        ModelState.AddModelError(string.Empty, "CPF/e-mail ou senha inválidos.");
        return Page();
    }
}
