using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public class ListaEsperaFormViewModel
{
    public int? PacienteId { get; set; }
    public int? MedicoId { get; set; }

    [Display(Name = "Especialidade")]
    public int? EspecialidadeId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data preferida")]
    public DateTime? DataPreferida { get; set; }

    [StringLength(20)]
    [Display(Name = "Período")]
    public string Periodo { get; set; } = "Qualquer";

    [StringLength(500)]
    [Display(Name = "Observação")]
    public string? Observacao { get; set; }
}
