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

    // Compatibilidade com a coluna legada. O fluxo atual usa StatusCobertura.
    public bool Coberta { get; set; } = true;

    [Required]
    [StringLength(24)]
    public string StatusCobertura { get; set; } = StatusCoberturaConvenio.VerificacaoManual;

    public bool RequerAutorizacao { get; set; }
    public bool Ativo { get; set; } = true;

    [StringLength(500)]
    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
