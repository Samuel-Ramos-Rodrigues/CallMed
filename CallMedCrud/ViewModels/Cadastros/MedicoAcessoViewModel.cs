using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public class MedicoAcessoViewModel
{
    public int MedicoId { get; set; }
    public string MedicoNome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail do médico.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
    public string? Senha { get; set; }

    public bool PossuiAcesso { get; set; }
}
