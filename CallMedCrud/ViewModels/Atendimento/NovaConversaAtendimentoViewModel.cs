using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.ViewModels;

public sealed class NovaConversaAtendimentoViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecione um paciente.")]
    [Display(Name = "Paciente")]
    public int PacienteId { get; set; }

    [Required(ErrorMessage = "Selecione o canal de atendimento.")]
    [Display(Name = "Canal")]
    public CanalAtendimento Canal { get; set; } = CanalAtendimento.WhatsApp;

    [StringLength(300)]
    [Display(Name = "Assunto")]
    public string? Assunto { get; set; }

    [Required(ErrorMessage = "Digite a mensagem inicial.")]
    [StringLength(4000, ErrorMessage = "A mensagem pode ter no máximo 4.000 caracteres.")]
    [Display(Name = "Mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    public IReadOnlyList<PacienteOpcaoAtendimentoViewModel> Pacientes { get; set; } =
        Array.Empty<PacienteOpcaoAtendimentoViewModel>();
}
