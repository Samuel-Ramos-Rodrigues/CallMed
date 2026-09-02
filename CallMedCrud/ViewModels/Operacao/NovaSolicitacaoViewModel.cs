using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.ViewModels;

public sealed class NovaSolicitacaoViewModel
{
    [Display(Name = "Paciente")]
    public int? PacienteId { get; set; }

    [Required(ErrorMessage = "Selecione o canal.")]
    [Display(Name = "Canal de entrada")]
    public CanalAtendimento Canal { get; set; } = CanalAtendimento.Presencial;

    [Display(Name = "Especialidade")]
    public int? EspecialidadeId { get; set; }

    [Display(Name = "Médico preferido")]
    public int? MedicoId { get; set; }

    [Display(Name = "Data preferida")]
    [DataType(DataType.Date)]
    public DateTime? DataPreferida { get; set; }

    [Display(Name = "Período")]
    public string PeriodoPreferido { get; set; } = "Qualquer";

    [StringLength(160)]
    [Display(Name = "Nome do contato")]
    public string? NomeContato { get; set; }

    [StringLength(40)]
    [Display(Name = "Telefone")]
    public string? TelefoneContato { get; set; }

    [StringLength(256)]
    [EmailAddress]
    [Display(Name = "E-mail")]
    public string? EmailContato { get; set; }

    [StringLength(1200)]
    [Display(Name = "Solicitação/observação")]
    public string? Observacao { get; set; }
}
