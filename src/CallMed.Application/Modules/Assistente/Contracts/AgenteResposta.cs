namespace MKSANCrud.DTOs.Agente;

public sealed class AgenteResposta
{
    public string Resposta { get; init; } = string.Empty;
    public bool EncerrarAtendimento { get; init; }
    public string SessionId { get; init; } = string.Empty;
}
