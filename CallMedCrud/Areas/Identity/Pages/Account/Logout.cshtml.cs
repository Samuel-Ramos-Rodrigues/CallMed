using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MKSANCrud.Data;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Areas.Identity.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<Usuario> _signInManager;

    public LogoutModel(SignInManager<Usuario> signInManager)
    {
        _signInManager = signInManager;
    }

    public IActionResult OnGet()
    {
        // Logout só é efetivado por POST.
        return LocalRedirect(Url.Content("~/"));
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var portal = PortaisAcesso.DoUsuario(User);
        await _signInManager.SignOutAsync();

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        var destino = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~" + portal.UrlLogin);

        return LocalRedirect(destino);
    }
}
