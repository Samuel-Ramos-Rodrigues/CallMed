using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public sealed class ExameHistorico
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    [Required, StringLength(160)]
    public string Nome { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime DataRealizacao { get; set; }

    [StringLength(160)]
    public string? LocalRealizacao { get; set; }

    public bool Ativo { get; set; } = true;

    [Required, StringLength(20)]
    public string StatusTriagem { get; set; } = ExameTriagemStatus.Pendente;

    public DateTime? TriadoEm { get; set; }

    [StringLength(450)]
    public string? TriadoPorUsuarioId { get; set; }

    [StringLength(600)]
    public string? ObservacaoTriagem { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
