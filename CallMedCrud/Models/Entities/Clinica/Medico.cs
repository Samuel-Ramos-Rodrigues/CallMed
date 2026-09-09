using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class Medico
{
    public int Id { get; set; }

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

    [Required(ErrorMessage = "Informe o nome do médico.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    // Mantido para compatibilidade com o banco legado. A fonte canônica é
    // EspecialidadeId/EspecialidadeCadastro e o controller mantém os dois em sincronia.
    [StringLength(100)]
    public string Especialidade { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Crm { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<Consulta> Consultas { get; set; } = new List<Consulta>();
    public ICollection<Disponibilidade> Disponibilidades { get; set; } = new List<Disponibilidade>();
    public ICollection<MedicoHorarioSemanal> HorariosSemanais { get; set; } = new List<MedicoHorarioSemanal>();
}
