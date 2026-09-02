using System.ComponentModel.DataAnnotations;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Models;

public class SolicitacaoAtendimento
{
    public int Id { get; set; }

    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public int? EspecialidadeId { get; set; }
    public Especialidade? Especialidade { get; set; }

    public int? MedicoId { get; set; }
    public Medico? Medico { get; set; }

    public int? ConsultaId { get; set; }
    public Consulta? Consulta { get; set; }

    public long? ConversaAtendimentoId { get; set; }
    public ConversaAtendimento? ConversaAtendimento { get; set; }

    public CanalAtendimento Canal { get; set; } = CanalAtendimento.Web;
    public StatusSolicitacaoAtendimento Status { get; set; } = StatusSolicitacaoAtendimento.Nova;

    [StringLength(160)]
    public string? NomeContato { get; set; }

    [StringLength(40)]
    public string? TelefoneContato { get; set; }

    [StringLength(256)]
    [EmailAddress]
    public string? EmailContato { get; set; }

    [StringLength(120)]
    public string? ConvenioInformado { get; set; }

    public bool? ElegivelConvenio { get; set; }

    [StringLength(600)]
    public string? PendenciaTriagem { get; set; }

    [StringLength(600)]
    public string? JustificativaLiberacao { get; set; }

    [StringLength(20)]
    public string PeriodoPreferido { get; set; } = "Qualquer";

    [DataType(DataType.Date)]
    public DateTime? DataPreferida { get; set; }

    [StringLength(1200)]
    public string? Observacao { get; set; }

    public string? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? TriadaEm { get; set; }
    public DateTime? AguardandoPacienteEm { get; set; }
    public DateTime? ConfirmadaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }
}
