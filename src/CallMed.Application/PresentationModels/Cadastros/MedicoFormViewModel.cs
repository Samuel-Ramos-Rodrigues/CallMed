using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public class MedicoFormViewModel
{
    public int Id { get; set; }
    [Required,StringLength(40)] public string AreaProfissional {get;set;} = "Medicina";
    [StringLength(20)] public string? RegistroConselho {get;set;}
    [StringLength(30)] public string? RegistroNumero {get;set;}
    [StringLength(2)] public string? RegistroUf {get;set;}


    [Required(ErrorMessage = "Informe o nome do profissional.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a especialidade.")]
    [StringLength(100)]
    [Display(Name = "Especialidade")]
    public string Especialidade { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "CRM")]
    public string? Crm { get; set; }

    [StringLength(30)]
    [Display(Name = "CRP")]
    public string? Crp { get; set; }
    [Range(5,240,ErrorMessage="Use duração entre 5 e 240 minutos.")]
    [Display(Name="Duração padrão da consulta (minutos)")]
    public int DuracaoPadraoMinutos { get; set; } = 30;

    public bool Ativo { get; set; } = true;

    public List<AgendaDiaViewModel> Agenda { get; set; } = AgendaDiaViewModel.CriarSemana();
}
