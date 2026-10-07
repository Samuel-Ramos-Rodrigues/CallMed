using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public sealed class ExameHistorico
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    [Required, StringLength(160)]
    public string Nome { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime DataRealizacao { get; set; }

    [StringLength(160)]
    public string? LocalRealizacao { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
