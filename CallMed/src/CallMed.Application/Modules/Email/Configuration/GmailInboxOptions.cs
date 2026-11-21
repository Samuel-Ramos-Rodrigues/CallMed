using System.Net.Mail;
using System.Globalization;

namespace MKSANCrud.Options;

/// <summary>Polling da caixa de atendimento pelo Gmail API. Use credenciais OAuth 2.0 no servidor.</summary>
public sealed class GmailInboxOptions
{
    public const string SectionName = "GmailInbox";

    public bool Enabled { get; set; }
    public string MailboxEmail { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    /// <summary>UTC instante a partir do qual e-mails podem ser importados. Impede importar a caixa inteira na primeira ativação.</summary>
    public string StartAfterUtc { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 90;
    public int MaxMessagesPerCycle { get; set; } = 20;
    public bool MarkProcessedAsRead { get; set; } = true;
    public string ProcessedLabelName { get; set; } = "CALLMED_PROCESSADO";

    public bool TryGetStartAfterUtc(out DateTimeOffset startAfterUtc) =>
        DateTimeOffset.TryParse(
            StartAfterUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out startAfterUtc);

    public bool Configurado
    {
        get
        {
            var email = MailboxEmail?.Trim();
            return Enabled &&
                !string.IsNullOrWhiteSpace(email) &&
                MailAddress.TryCreate(email, out var mailbox) &&
                string.Equals(mailbox.Address, email, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(ClientId) &&
                !string.IsNullOrWhiteSpace(ClientSecret) &&
                !string.IsNullOrWhiteSpace(RefreshToken) &&
                TryGetStartAfterUtc(out _) &&
                !string.IsNullOrWhiteSpace(ProcessedLabelName) &&
                ProcessedLabelName.Length <= 225;
        }
    }
}
