using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MKSANCrud.Data;

namespace MKSANCrud.Models;

public class Funcionario
{
    public int Id { get; set; }

    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [NotMapped]
    [DataType(DataType.Password)]
    public string? Senha { get; set; }

    [Required(ErrorMessage = "Informe o cargo.")]
    [StringLength(50)]
    [Display(Name = "Cargo")]
    public string Cargo { get; set; } = "Atendente";

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
