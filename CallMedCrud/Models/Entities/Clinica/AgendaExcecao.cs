using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class AgendaExcecao
{
    public int Id { get; set; }

    [Required]
    public int MedicoId { get; set; }
    public Medico? Medico { get; set; }

    [Required]
    [StringLength(20)]
    public string Tipo { get; set; } = AgendaExcecaoTipo.Bloqueio;

    [Required]
    [DataType(DataType.Date)]
    public DateTime Data { get; set; }

    [StringLength(5)]
    public string? HorarioInicio { get; set; }

    [StringLength(5)]
    public string? HorarioFim { get; set; }

    [StringLength(300)]
    public string? Motivo { get; set; }

    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? EncerradoEm { get; set; }
}
