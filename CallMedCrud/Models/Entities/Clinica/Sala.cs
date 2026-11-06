using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class Sala
{
    public int Id { get; set; }

    [Required]
    [StringLength(80)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;
}
