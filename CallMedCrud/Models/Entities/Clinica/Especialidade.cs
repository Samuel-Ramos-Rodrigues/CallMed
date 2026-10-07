using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class Especialidade
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public ICollection<Medico> Medicos { get; set; } = new List<Medico>();
}
