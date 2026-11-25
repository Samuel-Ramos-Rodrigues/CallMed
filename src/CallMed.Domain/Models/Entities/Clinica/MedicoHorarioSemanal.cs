using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class MedicoHorarioSemanal
{
    public int Id { get; set; }

    [Required]
    public int MedicoId { get; set; }

    public Medico? Medico { get; set; }

    [Range(0, 6)]
    public int DiaSemana { get; set; }

    [Required]
    [StringLength(5)]
    public string Horario { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
}
