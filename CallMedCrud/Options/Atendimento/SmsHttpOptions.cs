namespace MKSANCrud.Options;

/// <summary>
/// Adaptador HTTP genérico para SMS.
/// O endpoint deve aceitar JSON no formato:
/// { "to": "5579999999999", "from": "CallMed", "text": "mensagem" }.
/// </summary>
public sealed class SmsHttpOptions
{
    public const string SectionName = "Atendimento:Sms:Http";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string SendPath { get; set; } = "/messages";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = "Authorization";
    public string ApiKeyScheme { get; set; } = "Bearer";
    public string Sender { get; set; } = "CallMed";
    public string WebhookSecret { get; set; } = string.Empty;
}
