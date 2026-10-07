using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class ConvenioEspecialidade
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string ConvenioNome { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string ConvenioChave { get; set; } = string.Empty;

    public int EspecialidadeId { get; set; }
    public Especialidade? Especialidade { get; set; }

    public bool Coberta { get; set; } = true;
    public bool Ativo { get; set; } = true;

    [StringLength(500)]
    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
