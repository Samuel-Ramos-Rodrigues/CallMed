using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MKSANCrud.Areas.Identity.Pages.Account;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResetPasswordConfirmationModel : PageModel
{
    public string Mensagem { get; private set; } = string.Empty;
    public IActionResult OnGet()
    {
        if (TempData["SenhaRedefinida"] is not string mensagem || string.IsNullOrWhiteSpace(mensagem))
            return RedirectToPage("./ForgotPassword");
        Mensagem = mensagem;
        return Page();
    }
}
