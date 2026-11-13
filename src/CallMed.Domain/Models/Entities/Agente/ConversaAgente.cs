using System.ComponentModel.DataAnnotations;
using MKSANCrud.Data;

namespace MKSANCrud.Models;

public class ConversaAgente
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;
    public Usuario? Usuario { get; set; }

    [Required]
    [StringLength(120)]
    public string SessionId { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<MensagemConversaAgente> Mensagens { get; set; } = new List<MensagemConversaAgente>();
}
