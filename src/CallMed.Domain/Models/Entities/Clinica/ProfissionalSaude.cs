using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class ProfissionalSaude
{
    public int Id { get; set; }
    [StringLength(40)] public string AreaProfissional {get;set;} = "";
    [StringLength(20)] public string? RegistroConselho {get;set;}
    [StringLength(30)] public string? RegistroNumero {get;set;}
    [StringLength(2)] public string? RegistroUf {get;set;}
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool EhPsicologia => AreaProfissional=="Psicologia" || (AreaProfissional=="" && !string.IsNullOrWhiteSpace(Crp));
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string RegistroExibicao => EhPsicologia ? "CRP "+(Crp??"a conferir") : string.IsNullOrWhiteSpace(RegistroNumero)?"Registro a conferir":$"{RegistroConselho} {RegistroNumero}/{RegistroUf}";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HabilitadoPsicologia => EhPsicologia && HabilitadoClinicamente;


    public string? UsuarioId { get; set; }
    public MKSANCrud.Data.Usuario? Usuario { get; set; }

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256)]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Selecione a especialidade.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecione a especialidade.")]
    public int? EspecialidadeId { get; set; }
    public Especialidade? EspecialidadeCadastro { get; set; }

    [Required(ErrorMessage = "Informe o nome do profissional.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    // Mantido para compatibilidade com o banco legado. A fonte canônica é
    // EspecialidadeId/EspecialidadeCadastro e o controller mantém os dois em sincronia.
    [StringLength(100)]
    public string Especialidade { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Crm { get; set; }

    // CRP é independente do registro legado; nunca converter CRM automaticamente.
    [StringLength(30)]
    public string? Crp { get; set; }
    public bool ResponsavelTecnico {get;set;}
    public DateTime? RegistroVerificadoEm { get; set; }
    public string? RegistroVerificadoPor { get; set; }
    [StringLength(500)]
    public string? FonteVerificacaoRegistro { get; set; }
    public int DuracaoPadraoMinutos { get; set; } = 50;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HabilitadoClinicamente => Ativo && RegistroVerificadoEm != null && MKSANCrud.Services.Clinica.RegistroProfissional.Valido(this);

    public bool Ativo { get; set; } = true;

    public ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
    public ICollection<Disponibilidade> Disponibilidades { get; set; } = new List<Disponibilidade>();
    public ICollection<MedicoHorarioSemanal> HorariosSemanais { get; set; } = new List<MedicoHorarioSemanal>();
}
