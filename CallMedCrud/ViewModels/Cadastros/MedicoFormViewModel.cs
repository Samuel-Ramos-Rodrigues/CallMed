using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public class MedicoFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o nome do médico.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a especialidade.")]
    [StringLength(100)]
    [Display(Name = "Especialidade")]
    public string Especialidade { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "CRM")]
    public string? Crm { get; set; }

    public bool Ativo { get; set; } = true;

    public List<AgendaDiaViewModel> Agenda { get; set; } = AgendaDiaViewModel.CriarSemana();
}
