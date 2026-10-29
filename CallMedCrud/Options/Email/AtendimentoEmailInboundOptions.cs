namespace MKSANCrud.Options;

/// <summary>
/// Configura o recebimento de respostas de e-mail pelo Brevo Inbound Parsing.
/// O domínio de recebimento deve ser um subdomínio controlado pela clínica, com MX apontando para o Brevo.
/// </summary>
public sealed class AtendimentoEmailInboundOptions
{
    public const string SectionName = "Atendimento:Email:Inbound";

    public bool Enabled { get; set; }
    public string WebhookSecret { get; set; } = string.Empty;
    public string ReplyAddressSecret { get; set; } = string.Empty;
    public string ReplyDomain { get; set; } = string.Empty;
}
