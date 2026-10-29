using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
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
    public string EmailContaMascarado { get; private set; } = string.Empty;

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu CPF ou e-mail.")]
        public string Usuario { get; set; } = string.Empty;
    }

    public class ResetModel
    {
        [Required(ErrorMessage = "O link de recuperação está incompleto.")]
        [EmailAddress(ErrorMessage = "O link de recuperação contém um e-mail inválido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "O código de recuperação está ausente.")]
        public string Codigo { get; set; } = string.Empty;

        // Identificador da conta associado ao token. Evita depender apenas do e-mail
        // da query string em links novos; links antigos continuam usando o e-mail.
        public string UsuarioId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a nova senha.")]
        [MinLength(8, ErrorMessage = "A senha deve possuir pelo menos 8 caracteres.")]
        [DataType(DataType.Password)]
        public string NovaSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme a nova senha.")]
        [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não coincidem.")]
        [DataType(DataType.Password)]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? email = null, string? code = null, string? uid = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        Reset.Email = email?.Trim() ?? string.Empty;
        Reset.Codigo = code.Trim();
        Reset.UsuarioId = uid?.Trim() ?? string.Empty;

        // O token identifica a operação, o UID vincula a conta sem exigir que
        // o paciente digite novamente o CPF durante a redefinição.
        if (!string.IsNullOrWhiteSpace(Reset.UsuarioId))
        {
            var usuarioVinculado = await _userManager.FindByIdAsync(Reset.UsuarioId);
            if (usuarioVinculado is not null)
            {
                if (string.IsNullOrWhiteSpace(Reset.Email))
                    Reset.Email = usuarioVinculado.Email ?? string.Empty;
                EmailContaMascarado = MascararEmail(usuarioVinculado.Email ?? Reset.Email);
            }
        }
        else
        {
            EmailContaMascarado = MascararEmail(Reset.Email);
        }

        MostrarRedefinicao = true;
    }

    public async Task<IActionResult> OnPostGerarAsync()
    {
        ModelState.Clear();
        SolicitacaoEnviada = false;

        var identificador = Input?.Usuario?.Trim();
        if (string.IsNullOrWhiteSpace(identificador))
        {
            ModelState.AddModelError("Input.Usuario", "Informe seu CPF ou e-mail.");
            return Page();
        }

        // Resposta genérica para não revelar se uma conta existe.
        SolicitacaoEnviada = true;
        var usuario = await ResolverUsuarioAsync(identificador);
        var emailEntrega = usuario?.Email?.Trim();
        if (usuario is null || string.IsNullOrWhiteSpace(emailEntrega) ||
            !MailAddressValido(emailEntrega))
        {
            _logger.LogInformation("Solicitação de recuperação recebida; não foi encontrado um acesso elegível para envio.");
            return Page();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        // Base64URL impede que caracteres do token Identity sejam alterados na query string.
        var codigoSeguro = "v1_" + WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Page(
            "/Account/ForgotPassword",
            pageHandler: null,
            values: new { area = "Identity", email = emailEntrega, code = codigoSeguro, uid = usuario.Id },
            protocol: Request.Scheme);

        if (string.IsNullOrWhiteSpace(link))
        {
            _logger.LogWarning("Não foi possível gerar o link da recuperação de senha.");
            return Page();
        }

        var linkSeguro = WebUtility.HtmlEncode(link);
        var html = $"""
            <!doctype html>
            <html lang="pt-BR"><body style="margin:0;background:#edf3ed;font-family:Arial,Helvetica,sans-serif;color:#243b31;padding:28px 12px">
              <div style="max-width:560px;margin:0 auto;background:#f8faf5;border:1px solid #d1dfd2;border-radius:18px;overflow:hidden">
                <div style="background:#285d49;padding:24px 28px;color:#ffffff"><strong style="font-size:24px;letter-spacing:-.5px">CallMed</strong><div style="font-size:11px;letter-spacing:1.5px;margin-top:5px;color:#cfe6d4">CUIDADO EM CONEXÃO</div></div>
                <div style="padding:30px 28px"><p style="font-size:12px;letter-spacing:1.5px;text-transform:uppercase;color:#42765c;font-weight:bold">Recuperação de acesso</p><h1 style="font-size:25px;line-height:1.25;margin:8px 0 16px;color:#213c2e">Vamos redefinir sua senha?</h1>
                <p style="font-size:15px;line-height:1.7">Recebemos uma solicitação para redefinir a senha da sua conta CallMed. Use o botão abaixo para criar uma nova senha.</p>
                <p style="margin:26px 0"><a href="{linkSeguro}" style="display:inline-block;background:#34745a;color:#ffffff;text-decoration:none;font-weight:bold;padding:13px 22px;border-radius:10px">Redefinir minha senha</a></p>
                <p style="font-size:13px;line-height:1.6;color:#53695c">O link é temporário e de uso único. Se você não solicitou a alteração, ignore esta mensagem; sua senha não será modificada.</p></div>
                <div style="border-top:1px solid #dbe5dc;padding:16px 28px;background:#f0f5ee;color:#607468;font-size:11px">CallMed · Sua saúde, mais perto.</div>
              </div></body></html>
            """;

        var enviado = await _emailService.EnviarAsync(
            emailEntrega,
            "Redefinição de senha | CallMed",
            html,
            HttpContext.RequestAborted);

        if (!enviado)
        {
            _logger.LogWarning("O serviço de e-mail não confirmou o envio da recuperação de senha.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRedefinirAsync()
    {
        MostrarRedefinicao = true;
        EmailContaMascarado = MascararEmail(Reset?.Email);

        // O formulário de redefinição não envia os dados de Input (CPF/e-mail).
        // Removemos também a chave-pai, que pode manter um erro Required antigo no ModelState.
        foreach (var chave in ModelState.Keys
                     .Where(k => string.Equals(k, nameof(Input), StringComparison.OrdinalIgnoreCase) ||
                                 k.StartsWith(nameof(Input) + ".", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            ModelState.Remove(chave);
        }
        ModelState.Remove("Input");
        ModelState.Remove("Input.Usuario");

        if (!ModelState.IsValid)
            return Page();

        if (!TentarDecodificarCodigo(Reset.Codigo, out var token))
        {
            ModelState.AddModelError(string.Empty, "Este link de recuperação é inválido ou expirou. Solicite outro link.");
            return Page();
        }

        Usuario? usuario = null;
        if (!string.IsNullOrWhiteSpace(Reset.UsuarioId))
        {
            usuario = await _userManager.FindByIdAsync(Reset.UsuarioId.Trim());
            // O e-mail também fica preso ao link para detectar links manipulados ou
            // links antigos abertos depois de uma mudança de e-mail.
            if (usuario is not null && !string.IsNullOrWhiteSpace(Reset.Email))
            {
                var emailEsperado = _userManager.NormalizeEmail(Reset.Email.Trim());
                if (!string.Equals(usuario.NormalizedEmail, emailEsperado, StringComparison.Ordinal))
                    usuario = null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(Reset.Email))
        {
            // Compatibilidade com links já enviados antes da inclusão do UID.
            usuario = await _userManager.FindByEmailAsync(Reset.Email.Trim());
        }

        if (usuario is null)
        {
            ModelState.AddModelError(string.Empty, "Este link de recuperação é inválido ou expirou. Solicite outro link.");
            return Page();
        }

        var resultado = await _userManager.ResetPasswordAsync(usuario, token, Reset.NovaSenha);
        if (!resultado.Succeeded)
        {
            foreach (var erro in resultado.Errors)
                ModelState.AddModelError(string.Empty, TraduzirErroIdentity(erro.Code, erro.Description));
            return Page();
        }

        TempData["SenhaRedefinida"] = "Senha redefinida com sucesso. Entre com sua nova senha.";
        return RedirectToPage("./Login");
    }

    private async Task<Usuario?> ResolverUsuarioAsync(string identificador)
    {
        if (identificador.Contains('@'))
        {
            var usuarioPorEmail = await _userManager.FindByEmailAsync(identificador);
            if (usuarioPorEmail is not null)
                return usuarioPorEmail;

            // Alguns cadastros antigos têm o e-mail no paciente e o vínculo Identity
            // apenas pelo UsuarioId. Resolve o vínculo, mas entrega o link no e-mail real da conta.
            var pacientePorEmail = await _context.Pacientes.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Ativo && p.Email != null && p.Email.ToLower() == identificador.ToLower());
            if (pacientePorEmail is null)
                return null;

            if (!string.IsNullOrWhiteSpace(pacientePorEmail.UsuarioId))
                return await _userManager.FindByIdAsync(pacientePorEmail.UsuarioId);

            return pacientePorEmail.Email is null
                ? null
                : await _userManager.FindByEmailAsync(pacientePorEmail.Email);
        }

        var cpf = new string(identificador.Where(char.IsDigit).ToArray());
        if (cpf.Length != 11)
            return null;

        var paciente = await _context.Pacientes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Cpf == cpf && p.Ativo);
        if (paciente is null)
            return null;

        if (!string.IsNullOrWhiteSpace(paciente.UsuarioId))
        {
            var usuarioVinculado = await _userManager.FindByIdAsync(paciente.UsuarioId);
            if (usuarioVinculado is not null)
                return usuarioVinculado;
        }

        return string.IsNullOrWhiteSpace(paciente.Email)
            ? null
            : await _userManager.FindByEmailAsync(paciente.Email);
    }

    private static bool TentarDecodificarCodigo(string? codigo, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 8192)
            return false;

        if (!codigo.StartsWith("v1_", StringComparison.Ordinal))
        {
            // Aceita links emitidos por versões antigas enquanto ainda estiverem válidos.
            token = codigo;
            return true;
        }

        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(codigo[3..]));
            return !string.IsNullOrWhiteSpace(token);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string MascararEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return string.Empty;
        var partes = email.Trim().Split('@', 2);
        if (partes.Length != 2 || partes[0].Length == 0)
            return string.Empty;
        var inicio = partes[0].Length <= 2 ? partes[0][..1] : partes[0][..2];
        return $"{inicio}{new string('•', Math.Clamp(partes[0].Length - inicio.Length, 3, 8))}@{partes[1]}";
    }

    private static bool MailAddressValido(string email) =>
        System.Net.Mail.MailAddress.TryCreate(email, out var parsed) &&
        string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);

    private static string TraduzirErroIdentity(string codigo, string descricao) => codigo switch
    {
        "PasswordTooShort" => "A senha é muito curta. Use pelo menos 8 caracteres.",
        "PasswordRequiresDigit" => "A senha precisa conter pelo menos um número.",
        "PasswordRequiresUpper" => "A senha precisa conter uma letra maiúscula.",
        "PasswordRequiresLower" => "A senha precisa conter uma letra minúscula.",
        "PasswordRequiresNonAlphanumeric" => "A senha precisa conter um caractere especial.",
        "PasswordRequiresSpecialCharacter" => "A senha precisa conter pelo menos um símbolo ou sinal de pontuação (por exemplo, !, # ou @). Espaços não contam.",
        "InvalidToken" => "Este link é inválido ou expirou. Solicite outro link.",
        _ => descricao
    };
}
