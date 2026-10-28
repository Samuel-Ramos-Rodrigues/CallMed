namespace MKSANCrud.Options;

/// <summary>Configuração do transporte SMTP (Brevo recomendado).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp-relay.brevo.com";
    public int Port { get; set; } = 2525;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "CallMed";
    /// <summary>Usa STARTTLS; porta 2525 é utilizada para SMTP com TLS no Brevo/Render.</summary>
    public bool EnableSsl { get; set; } = true;
}
