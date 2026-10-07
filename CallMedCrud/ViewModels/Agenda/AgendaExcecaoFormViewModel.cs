using MKSANCrud.Models;
using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public sealed class AgendaExcecaoFormViewModel
{
    [Required(ErrorMessage = "Selecione o médico.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o médico.")]
    public int MedicoId { get; set; }

    [Required(ErrorMessage = "Selecione o tipo de exceção.")]
    public string Tipo { get; set; } = AgendaExcecaoTipo.Bloqueio;

    [Required(ErrorMessage = "Informe a data.")]
    [DataType(DataType.Date)]
    public DateTime? Data { get; set; }

    [Display(Name = "Horário")]
    public string? HorarioInicio { get; set; }

    [Display(Name = "Até")]
    public string? HorarioFim { get; set; }

    [StringLength(300)]
    public string? Motivo { get; set; }
}
