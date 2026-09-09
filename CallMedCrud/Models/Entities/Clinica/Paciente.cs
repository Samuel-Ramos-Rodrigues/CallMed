using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MKSANCrud.Data;

namespace MKSANCrud.Models;

public class Paciente
{
    public int Id { get; set; }

    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF.")]
    [StringLength(11, MinimumLength = 11)]
    public string Cpf { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [NotMapped]
    [DataType(DataType.Password)]
    public string? Senha { get; set; }

    [StringLength(25)]
    public string? Telefone { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DataNascimento { get; set; }

    public bool TemConvenio { get; set; }

    [StringLength(120)]
    public string? NomeConvenio { get; set; }

    [StringLength(80)]
    public string? NumeroConvenio { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ValidadeConvenio { get; set; }

    [StringLength(20)]
    public string CanalPreferido { get; set; } = "WhatsApp";

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
}
