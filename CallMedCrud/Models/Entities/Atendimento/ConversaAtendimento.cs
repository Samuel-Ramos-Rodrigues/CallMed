using System.ComponentModel.DataAnnotations;
using MKSANCrud.Data;

namespace MKSANCrud.Models.Atendimento;

public class ConversaAtendimento
{
    public long Id { get; set; }

    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public CanalAtendimento Canal { get; set; }

    [Required]
    [StringLength(320)]
    public string IdentificadorExterno { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string SessionId { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Assunto { get; set; }

    public ModoAtendimento Modo { get; set; } = ModoAtendimento.IA;

    public string? ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    public DateTime UltimaInteracaoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AssumidaEm { get; set; }
    public DateTime? VisualizadaEm { get; set; }

    public ICollection<MensagemAtendimento> Mensagens { get; set; } =
        new List<MensagemAtendimento>();
}
