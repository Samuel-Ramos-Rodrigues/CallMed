namespace MKSANCrud.Options;

public sealed class SmsHttpOptions
{
    public const string SectionName = "Atendimento:Sms:Http";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } =
        "https://api.sms-gate.app/3rdparty/v1";

    public string SendPath { get; set; } =
        "/messages";

    public string Username { get; set; } =
        string.Empty;

    public string Password { get; set; } =
        string.Empty;

    public string WebhookSecret { get; set; } =
        string.Empty;
}
