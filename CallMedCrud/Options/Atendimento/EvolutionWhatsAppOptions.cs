namespace MKSANCrud.Options;

public sealed class EvolutionWhatsAppOptions
{
    public const string SectionName = "Atendimento:WhatsApp:Evolution";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "mksan";
    public string PublicNumber { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}
