using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class MensagemConversaAgente
{
    public long Id { get; set; }

    public int ConversaAgenteId { get; set; }
    public ConversaAgente? Conversa { get; set; }

    [Required]
    [StringLength(20)]
    public string Papel { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Texto { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
