namespace MKSANCrud.DTOs.Agente;

public sealed class MensagemAgenteRequest
{
    public string Mensagem { get; set; } = string.Empty;
    public string? SessionId { get; set; }

    // Usado somente para reconstruir contexto depois de restart/sleep do servidor.
    // O backend trata esse conteúdo como não confiável e nunca como instrução de sistema.
    public List<MensagemHistoricoAgente> Historico { get; set; } = [];
}
