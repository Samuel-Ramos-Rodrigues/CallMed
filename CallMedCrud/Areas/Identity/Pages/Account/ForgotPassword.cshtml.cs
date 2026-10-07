using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Email;

namespace MKSANCrud.Areas.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<Usuario> _userManager;
    private readonly MKSANContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        UserManager<Usuario> userManager,
        MKSANContext context,
        IEmailService emailService,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public ResetModel Reset { get; set; } = new();

    public bool MostrarRedefinicao { get; set; }
    public bool SolicitacaoEnviada { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu CPF ou e-mail.")]
        public string Usuario { get; set; } = string.Empty;
    }

    public class ResetModel
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o código de recuperação.")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a nova senha.")]
        [MinLength(8, ErrorMessage = "A senha deve possuir pelo menos 8 caracteres.")]
        [DataType(DataType.Password)]
        public string NovaSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme a nova senha.")]
        [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem.")]
        [DataType(DataType.Password)]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }

    public void OnGet(string? email = null, string? code = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return;

        Reset.Email = email;
        Reset.Codigo = code;
        MostrarRedefinicao = true;
    }

    public async Task<IActionResult> OnPostGerarAsync()
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(Input.Usuario))
        {
            ModelState.AddModelError(nameof(Input.Usuario), "Informe seu CPF ou e-mail.");
            return Page();
        }

        var valor = Input.Usuario.Trim();
        var email = await ResolverEmailAsync(valor);

        // Resposta genérica para não revelar se a conta existe.
        SolicitacaoEnviada = true;

        if (string.IsNullOrWhiteSpace(email))
            return Page();

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Page();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var link = Url.Page(
            "/Account/ForgotPassword",
            pageHandler: null,
            values: new { area = "Identity", email, code = token },
            protocol: Request.Scheme);

        if (string.IsNullOrWhiteSpace(link))
            return Page();

        var html = $"""
            <h2>Redefinição de senha - CallMed</h2>
            <p>Recebemos uma solicitação para redefinir sua senha.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Clique aqui para criar uma nova senha</a>.</p>
            <p>Se você não fez essa solicitação, ignore este e-mail.</p>
            """;

        var enviado = await _emailService.EnviarAsync(
            email,
            "Redefinição de senha - CallMed",
            html,
            HttpContext.RequestAborted);

        if (!enviado)
        {
            _logger.LogWarning(
                "Solicitação de recuperação processada, mas o SMTP não enviou a mensagem.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRedefinirAsync()
    {
        MostrarRedefinicao = true;

        // O formulário de redefinição não envia Input.Usuario. Remove apenas
        // validações do formulário de solicitação antes de validar Reset.*.
        foreach (var chave in ModelState.Keys
                     .Where(k => k.StartsWith("Input.", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            ModelState.Remove(chave);
        }

        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Reset.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Link inválido ou expirado.");
            return Page();
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            Reset.Codigo,
            Reset.NovaSenha);

        if (!result.Succeeded)
        {
            foreach (var erro in result.Errors)
                ModelState.AddModelError(string.Empty, erro.Description);

            return Page();
        }

        TempData["SenhaRedefinida"] = "Senha redefinida. Entre com sua nova senha.";
        return RedirectToPage("./Login");
    }

    private async Task<string?> ResolverEmailAsync(string identificador)
    {
        if (identificador.Contains('@'))
            return identificador.Trim();

        var cpf = new string(identificador.Where(char.IsDigit).ToArray());
        if (cpf.Length != 11)
            return null;

        return await _context.Pacientes
            .AsNoTracking()
            .Where(p => p.Cpf == cpf && p.Ativo)
            .Select(p => p.Email)
            .FirstOrDefaultAsync();
    }
}
