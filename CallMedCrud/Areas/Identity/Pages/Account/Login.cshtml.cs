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

    // O parâmetro de perfil continua tolerado nas rotas antigas /acesso/{perfil},
    // mas não é usado para escolher permissões nem o tipo de conta autenticada.
    [BindProperty(SupportsGet = true)]
    public string? Perfil { get; set; }

    public string ReturnUrl { get; private set; } = "/";

    public class InputModel
    {
        [Required(ErrorMessage = "Informe o CPF do paciente ou o e-mail cadastrado.")]
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
        // URL canônica única. Rotas antigas/convencionais redirecionam para cá;
        // o perfil passado no caminho nunca seleciona uma modalidade de login.
        var caminhoAtual = (Request.Path.Value ?? string.Empty).TrimEnd('/');
        if (!string.Equals(caminhoAtual, "/acesso", StringComparison.OrdinalIgnoreCase))
            return RedirecionarParaEntradaUnica(returnUrl, contaInativa);

        PrepararReturnUrl(returnUrl);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (User.Identity?.IsAuthenticated == true)
            return RedirecionarParaPainelAtual();

        if (contaInativa)
            ModelState.AddModelError(string.Empty,
                "Seu acesso foi desativado. Entre em contato com o administrador da clínica.");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        PrepararReturnUrl(returnUrl);
        if (User.Identity?.IsAuthenticated == true)
            return RedirecionarParaPainelAtual();
        if (!ModelState.IsValid) return Page();

        var acesso = await _acessos.ObterContaUnificadaAsync(Input.Usuario);
        if (acesso.Usuario is null || acesso.Portal is null) return LoginInvalido();

        var result = await _signInManager.PasswordSignInAsync(
            acesso.Usuario, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!acesso.Portal.EhPaciente)
                return RedirectToAction("Index", acesso.Portal.ControllerPainel, new { area = "" });
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

    private void PrepararReturnUrl(string? returnUrl)
    {
        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
    }

    private IActionResult RedirecionarParaEntradaUnica(string? returnUrl, bool contaInativa)
    {
        var destino = Url.Content("~/acesso");
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            parametros.Add("returnUrl=" + Uri.EscapeDataString(returnUrl));
        if (contaInativa)
            parametros.Add("contaInativa=true");
        if (parametros.Count > 0)
            destino += "?" + string.Join("&", parametros);
        return Redirect(destino);
    }

    private IActionResult RedirecionarParaPainelAtual()
    {
        var portal = PortaisAcesso.DoUsuario(User);
        return RedirectToAction("Index", portal.ControllerPainel, new { area = "" });
    }

    private IActionResult LoginInvalido()
    {
        // Mensagem uniforme para não revelar se e-mail/CPF está cadastrado ou ativo.
        ModelState.AddModelError(string.Empty,
            "CPF/e-mail ou senha inválidos. Confira os dados e tente novamente.");
        return Page();
    }
}
