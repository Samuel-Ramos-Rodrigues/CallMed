using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class Consulta
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um paciente.")]
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione um médico.")]
    public int MedicoId { get; set; }
    public Medico? Medico { get; set; }

    [Required(ErrorMessage = "Selecione uma data.")]
    [DataType(DataType.Date)]
    public DateTime Data { get; set; }

    [Required(ErrorMessage = "Selecione um horário.")]
    [StringLength(8)]
    public string Horario { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = ConsultaStatus.Pendente;

    [Required]
    [StringLength(20)]
    public string TipoPagamento { get; set; } = TipoPagamentoConsulta.Particular;

    [StringLength(120)]
    public string? ConvenioUsado { get; set; }

    [StringLength(1000)]
    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmadaEm { get; set; }
    public DateTime? CanceladaEm { get; set; }
    public DateTime? RealizadaEm { get; set; }
    public DateTime? AusenteEm { get; set; }
    public DateTime? Lembrete24hEnviadoEm { get; set; }
    public DateTime? Lembrete2hEnviadoEm { get; set; }
}
