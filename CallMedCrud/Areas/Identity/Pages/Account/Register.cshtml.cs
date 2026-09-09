using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<Usuario> _userManager;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;

    public RegisterModel(
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager,
        MKSANContext context,
        IClinicaClock clock)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _clock = clock;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; set; } = "/";

    public class InputModel
    {
        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(150)]
        [Display(Name = "Nome completo")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu CPF.")]
        [Display(Name = "CPF")]
        public string Cpf { get; set; } = string.Empty;

        [StringLength(25)]
        [Display(Name = "Telefone")]
        public string? Telefone { get; set; }

        [Display(Name = "Data de nascimento")]
        [DataType(DataType.Date)]
        public DateTime? DataNascimento { get; set; }

        [Display(Name = "Possui convênio?")]
        public bool TemConvenio { get; set; }

        [StringLength(120)]
        [Display(Name = "Nome do convênio")]
        public string? NomeConvenio { get; set; }

        [StringLength(80)]
        [Display(Name = "Número da carteirinha")]
        public string? NumeroConvenio { get; set; }

        [Display(Name = "Validade do convênio")]
        [DataType(DataType.Date)]
        public DateTime? ValidadeConvenio { get; set; }

        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(256)]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe uma senha.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme sua senha.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "As senhas não coincidem.")]
        [Display(Name = "Confirmar senha")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");
        ReturnUrl = returnUrl;

        var cpf = CadastroValidator.SomenteNumeros(Input.Cpf);
        var email = Input.Email.Trim();

        if (!CadastroValidator.CpfValido(cpf))
            ModelState.AddModelError("Input.Cpf", "Informe um CPF válido.");

        if (!CadastroValidator.DataNascimentoValida(Input.DataNascimento, _clock.Hoje))
            ModelState.AddModelError("Input.DataNascimento", "Informe uma data de nascimento válida.");

        if (Input.TemConvenio)
        {
            if (string.IsNullOrWhiteSpace(Input.NomeConvenio))
                ModelState.AddModelError("Input.NomeConvenio", "Informe o nome do convênio.");

            if (Input.ValidadeConvenio.HasValue && Input.ValidadeConvenio.Value.Date < _clock.Hoje)
                ModelState.AddModelError("Input.ValidadeConvenio", "A validade do convênio está vencida.");
        }

        if (!ModelState.IsValid)
            return Page();

        var pacienteCpf = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.Cpf == cpf);

        var pacienteEmail = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());

        if (pacienteCpf is not null &&
            !string.Equals(pacienteCpf.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Input.Cpf", "Este CPF já está cadastrado com outro e-mail.");
            return Page();
        }

        if (pacienteEmail is not null && pacienteEmail.Cpf != cpf)
        {
            ModelState.AddModelError("Input.Email", "Este e-mail já está cadastrado para outro paciente.");
            return Page();
        }

        if (await _context.Medicos.AnyAsync(m => m.Email != null && m.Email.ToLower() == email.ToLower()))
        {
            ModelState.AddModelError("Input.Email", "Este e-mail está reservado para um acesso médico.");
            return Page();
        }

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError("Input.Email", "Este e-mail já possui uma conta.");
            return Page();
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        var user = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            await tx.RollbackAsync();
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Paciente");
        if (!roleResult.Succeeded)
        {
            await tx.RollbackAsync();
            foreach (var error in roleResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        var paciente = pacienteCpf ?? pacienteEmail;
        if (paciente is null)
        {
            paciente = new Paciente
            {
                Cpf = cpf,
                CriadoEm = DateTime.UtcNow
            };
            _context.Pacientes.Add(paciente);
        }

        paciente.UsuarioId = user.Id;
        paciente.Ativo = true;
        paciente.Nome = Input.Nome.Trim();
        paciente.Email = email;
        paciente.Telefone = Input.Telefone?.Trim();
        paciente.DataNascimento = Input.DataNascimento?.Date;
        paciente.TemConvenio = Input.TemConvenio;
        paciente.NomeConvenio = Input.TemConvenio ? Input.NomeConvenio?.Trim() : null;
        paciente.NumeroConvenio = Input.TemConvenio ? Input.NumeroConvenio?.Trim() : null;
        paciente.ValidadeConvenio = Input.TemConvenio ? Input.ValidadeConvenio?.Date : null;

        try
        {
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }
        catch
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(
                string.Empty,
                "Não foi possível concluir o cadastro. Verifique os dados e tente novamente.");
            return Page();
        }
    }
}
