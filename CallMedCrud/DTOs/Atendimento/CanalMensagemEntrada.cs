using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.DTOs.Atendimento;

public sealed class CanalMensagemEntrada
{
    public CanalAtendimento Canal { get; init; }
    public string Identificador { get; init; } = string.Empty;
    public string Texto { get; init; } = string.Empty;
    public string? MensagemExternaId { get; init; }
    public string? Assunto { get; init; }
}
