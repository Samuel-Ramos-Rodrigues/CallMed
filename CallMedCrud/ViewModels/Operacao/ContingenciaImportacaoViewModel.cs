using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public sealed class ContingenciaImportacaoViewModel
{
    public Guid Chave { get; set; }
    public DateTimeOffset CapturadaEm { get; set; }

    [Required, StringLength(160, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Telefone { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Required, StringLength(120)]
    public string Especialidade { get; set; } = string.Empty;

    [Required, RegularExpression("^(Presencial|Telefone)$")]
    public string Canal { get; set; } = "Presencial";

    [Required, RegularExpression("^(Qualquer|Manha|Tarde|Noite)$")]
    public string Periodo { get; set; } = "Qualquer";

    [DataType(DataType.Date)]
    public DateTime? DataPreferida { get; set; }
}
