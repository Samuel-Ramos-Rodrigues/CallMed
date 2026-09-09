using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public class MinhaContaViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [Display(Name = "Nome")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF.")]
    [Display(Name = "CPF")]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Telefone")]
    [StringLength(30)]
    public string? Telefone { get; set; }

    [Display(Name = "Data de nascimento")]
    public DateTime? DataNascimento { get; set; }

    [Display(Name = "Possui convênio")]
    public bool TemConvenio { get; set; }

    [Display(Name = "Nome do convênio")]
    [StringLength(120)]
    public string? NomeConvenio { get; set; }

    [Display(Name = "Número da carteirinha")]
    [StringLength(80)]
    public string? NumeroConvenio { get; set; }

    [Display(Name = "Validade do convênio")]
    public DateTime? ValidadeConvenio { get; set; }

    [Display(Name = "Canal preferido para lembretes")]
    [StringLength(20)]
    public string CanalPreferido { get; set; } = "WhatsApp";

    public DateTime CriadoEm { get; set; }
}
