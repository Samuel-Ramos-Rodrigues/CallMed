namespace MKSANCrud.Models.Atendimento;

/// <summary>Uma avaliação por encerramento, identificada pela última entrada do paciente.</summary>
public sealed class AvaliacaoAtendimento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long ConversaAtendimentoId { get; set; }
    public ConversaAtendimento? Conversa { get; set; }
    // Snapshot sem FK: o histórico de mensagens pode ser aparado.
    public long UltimaEntradaId { get; set; }
    public CanalAtendimento Canal { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public int? Nota { get; set; }
    public string? Comentario { get; set; }
    public DateTime? RespondidoEm { get; set; }
    public DateTime? DispensadaEm { get; set; }
    public string? RespostaExternaId { get; set; }
    public DateTime? ConviteProcessadoEm { get; set; }
    public DateTime? AgradecimentoProcessadoEm { get; set; }
    public bool OrientacaoPendente { get; set; }
    public DateTime? OrientacaoProcessadaEm { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
}
