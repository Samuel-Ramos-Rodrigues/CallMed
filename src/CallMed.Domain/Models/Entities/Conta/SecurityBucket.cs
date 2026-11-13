namespace MKSANCrud.Models;
public sealed class SecurityBucket
{
    public string Id { get; set; } = "";
    public int Contagem { get; set; }
    public DateTime ExpiraEm { get; set; }
}

public sealed class AtendimentoLeaseRow { public long ConversaId { get; set; } public Guid Token { get; set; } public DateTime Ate { get; set; } }
