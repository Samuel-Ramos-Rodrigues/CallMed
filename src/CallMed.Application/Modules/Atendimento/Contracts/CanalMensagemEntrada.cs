using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.DTOs.Atendimento;

public sealed class CanalMensagemEntrada
{
    public CanalAtendimento Canal { get; init; }
    public string Identificador { get; init; } = string.Empty;
    public string Texto { get; init; } = string.Empty;
    public string? MensagemExternaId { get; init; }
    public string? Assunto { get; init; }
    /// <summary>Usado apenas por webhooks autenticados que respondem a uma conversa já existente.</summary>
    public long? ConversaAtendimentoId { get; init; }
}
