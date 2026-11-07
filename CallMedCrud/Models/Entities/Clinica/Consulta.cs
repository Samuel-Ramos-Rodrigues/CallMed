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

    [Required]
    [StringLength(32)]
    public string StatusElegibilidadeConvenio { get; set; } = ElegibilidadeConvenioStatus.NaoAplicavel;

    [StringLength(600)]
    public string? ObservacaoElegibilidadeConvenio { get; set; }

    [StringLength(120)]
    public string? ProtocoloAutorizacaoConvenio { get; set; }

    public DateTime? ElegibilidadeConvenioAtualizadaEm { get; set; }

    [StringLength(450)]
    public string? ElegibilidadeConvenioAtualizadaPorUsuarioId { get; set; }

    [StringLength(1000)]
    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmadaEm { get; set; }
    public DateTime? CanceladaEm { get; set; }
    public DateTime? RealizadaEm { get; set; }
    public DateTime? AusenteEm { get; set; }
    public DateTime? CheckInEm { get; set; }
    [StringLength(20)]
    public string Modalidade { get; set; } = ModalidadeConsulta.Presencial;
    public int? SalaId { get; set; }
    public DateTime? TelemedicinaConsentimentoEm { get; set; }

    // Sala de espera (CFM 2.314/2022): o paciente entra na espera; a equipe o admite,
    // inicia e encerra o atendimento. O registro clínico do atendimento fica cifrado (enc:v1:).
    public DateTime? TelemedicinaSalaEsperaEm { get; set; }
    public DateTime? TelemedicinaAdmitidoEm { get; set; }
    public DateTime? TelemedicinaInicioEm { get; set; }
    public DateTime? TelemedicinaFimEm { get; set; }
    public string? TelemedicinaRegistro { get; set; }
    public DateTime? Lembrete24hEnviadoEm { get; set; }
    public DateTime? Lembrete2hEnviadoEm { get; set; }
}
