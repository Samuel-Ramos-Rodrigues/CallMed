namespace MKSANCrud.Options;

public sealed class SmsHttpOptions
{
    public const string SectionName = "Atendimento:Sms:Http";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } =
        "https://api.httpsms.com/v1";

    public string SendPath { get; set; } =
        "/messages/send";

    public string ApiKey { get; set; } =
        string.Empty;

    public string From { get; set; } =
        string.Empty;

    public string WebhookSecret { get; set; } =
        string.Empty;
}
