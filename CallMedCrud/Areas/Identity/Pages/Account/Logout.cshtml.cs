using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MKSANCrud.Data;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Areas.Identity.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly SignInManager<Usuario> _signInManager;
    private readonly ContaService? _conta;
    private readonly ILogger<LogoutModel>? _logger;

    public LogoutModel(SignInManager<Usuario> signInManager, ContaService? conta = null, ILogger<LogoutModel>? logger = null)
    {
        _signInManager = signInManager;
        _conta = conta;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        // Logout só é efetivado por POST.
        return LocalRedirect(Url.Content("~/"));
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var portal = PortaisAcesso.DoUsuario(User);
        if (_conta is not null)
        {
            try { await _conta.RegistrarSaidaAsync(User, HttpContext.RequestAborted); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Não foi possível registrar os metadados da saída."); }
        }
        await _signInManager.SignOutAsync();

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        var destino = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~" + portal.UrlLogin);

        return LocalRedirect(destino);
    }
}
