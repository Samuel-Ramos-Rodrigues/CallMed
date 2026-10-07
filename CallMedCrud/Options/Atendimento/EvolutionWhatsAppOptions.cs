namespace MKSANCrud.Options;

public sealed class EvolutionWhatsAppOptions
{
    public const string SectionName = "Atendimento:WhatsApp:Evolution";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "callmed";
    public string PublicNumber { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int IntervaloMinimoEnvioMs { get; set; } = 2000;
    public int EsperaApos429Segundos { get; set; } = 60;
}
