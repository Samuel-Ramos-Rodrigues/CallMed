namespace MKSANCrud.Options;

public sealed class EmailInboundOptions
{
    public const string SectionName = "Atendimento:Email:Inbound";

    public bool Enabled { get; set; }
    public string WebhookSecret { get; set; } = string.Empty;
}
