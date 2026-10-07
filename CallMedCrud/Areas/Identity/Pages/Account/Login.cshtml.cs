using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MKSANCrud.Data;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<Usuario> _signInManager;
    private readonly PortalAcessoService _acessos;

    public LoginModel(SignInManager<Usuario> signInManager, PortalAcessoService acessos)
    {
        _signInManager = signInManager;
        _acessos = acessos;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Perfil { get; set; }

    public PortalAcesso Portal { get; private set; } = PortaisAcesso.Paciente;
    public string ReturnUrl { get; private set; } = "/";

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu CPF ou e-mail de acesso.")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe sua senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Lembrar acesso")]
        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null, bool contaInativa = false)
    {
        if (!PrepararPortal(returnUrl)) return NotFound();
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", PortaisAcesso.DoUsuario(User).ControllerPainel, new { area = "" });
        if (contaInativa)
            ModelState.AddModelError(string.Empty, "Seu acesso foi desativado. Entre em contato com o administrador da clínica.");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!PrepararPortal(returnUrl)) return NotFound();
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", PortaisAcesso.DoUsuario(User).ControllerPainel, new { area = "" });
        if (!ModelState.IsValid) return Page();

        var usuario = await _acessos.ObterContaAsync(Portal, Input.Usuario);
        if (usuario is null) return LoginInvalido();
        var result = await _signInManager.PasswordSignInAsync(
            usuario, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!Portal.EhPaciente)
                return RedirectToAction("Index", Portal.ControllerPainel, new { area = "" });
            return LocalRedirect(ReturnUrl);
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Muitas tentativas incorretas. Aguarde alguns minutos e tente novamente.");
            return Page();
        }
        return LoginInvalido();
    }

    private bool PrepararPortal(string? returnUrl)
    {
        var portal = PortaisAcesso.Obter(Perfil);
        if (portal is null) return false;
        Portal = portal;
        Perfil = portal.Chave;
        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl : Url.Content("~/");
        return true;
    }

    private IActionResult LoginInvalido()
    {
        ModelState.AddModelError(string.Empty, Portal.EhPaciente
            ? "CPF/e-mail ou senha inválidos para a área do paciente."
            : "E-mail ou senha inválidos para esta área. Use o acesso fornecido pelo administrador.");
        return Page();
    }
}
