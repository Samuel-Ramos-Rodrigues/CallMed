using System.ComponentModel.DataAnnotations;
using MKSANCrud.Data;

namespace MKSANCrud.Models.Atendimento;

public class MensagemAtendimento
{
    public long Id { get; set; }

    public long ConversaAtendimentoId { get; set; }
    public ConversaAtendimento? Conversa { get; set; }

    public DirecaoMensagemAtendimento Direcao { get; set; }
    public AutorMensagemAtendimento Autor { get; set; }
    public StatusMensagemAtendimento Status { get; set; }

    [StringLength(220)]
    public string? MensagemExternaId { get; set; }

    [Required]
    [StringLength(5000)]
    public string Texto { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Erro { get; set; }

    public string? AutorUsuarioId { get; set; }
    public Usuario? AutorUsuario { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? EnviadoEm { get; set; }
}
