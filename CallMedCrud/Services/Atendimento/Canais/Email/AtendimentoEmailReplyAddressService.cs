using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Atendimento.Canais.Email;

/// <summary>
/// Gera endereços Reply-To exclusivos por conversa. O identificador da conversa
/// é protegido por HMAC para não permitir escolher conversas arbitrárias no webhook.
/// </summary>
public sealed class AtendimentoEmailReplyAddressService
{
    private static readonly Regex LocalPartRegex = new(
        @"^c(?<id>[1-9][0-9]*)-(?<sig>[a-fA-F0-9]{24})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AtendimentoEmailInboundOptions _options;

    public AtendimentoEmailReplyAddressService(IOptions<AtendimentoEmailInboundOptions> options)
    {
        _options = options.Value;
    }

    public bool Configurado =>
        _options.Enabled &&
        DominioValido(_options.ReplyDomain) &&
        !string.IsNullOrWhiteSpace(_options.WebhookSecret) &&
        Encoding.UTF8.GetByteCount(_options.WebhookSecret.Trim()) >= 32 &&
        !string.IsNullOrWhiteSpace(_options.ReplyAddressSecret) &&
        Encoding.UTF8.GetByteCount(_options.ReplyAddressSecret.Trim()) >= 32;

    public string? CriarEndereco(long conversaId)
    {
        if (!Configurado || conversaId <= 0)
            return null;

        var dominio = NormalizarDominio(_options.ReplyDomain);
        var assinatura = GerarAssinatura(conversaId);
        return $"c{conversaId}-{assinatura}@{dominio}";
    }

    public bool ValidarWebhookSecret(string? recebido)
    {
        if (!Configurado || string.IsNullOrWhiteSpace(recebido))
            return false;

        var esperadoBytes = Encoding.UTF8.GetBytes(_options.WebhookSecret.Trim());
        var recebidoBytes = Encoding.UTF8.GetBytes(recebido.Trim());
        return esperadoBytes.Length == recebidoBytes.Length &&
            CryptographicOperations.FixedTimeEquals(esperadoBytes, recebidoBytes);
    }

    public bool TentarObterConversaId(string? endereco, out long conversaId)
    {
        conversaId = 0;
        if (!Configurado || string.IsNullOrWhiteSpace(endereco) ||
            !MailAddress.TryCreate(endereco.Trim(), out var mailbox))
            return false;

        if (!string.Equals(mailbox.Host, NormalizarDominio(_options.ReplyDomain), StringComparison.OrdinalIgnoreCase))
            return false;

        var match = LocalPartRegex.Match(mailbox.User);
        if (!match.Success || !long.TryParse(match.Groups["id"].Value, out conversaId) || conversaId <= 0)
        {
            conversaId = 0;
            return false;
        }

        var esperado = GerarAssinatura(conversaId);
        var recebido = match.Groups["sig"].Value.ToLowerInvariant();
        var esperadoBytes = Encoding.ASCII.GetBytes(esperado);
        var recebidoBytes = Encoding.ASCII.GetBytes(recebido);
        if (esperadoBytes.Length != recebidoBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(esperadoBytes, recebidoBytes))
        {
            conversaId = 0;
            return false;
        }

        return true;
    }

    private string GerarAssinatura(long conversaId)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ReplyAddressSecret.Trim()));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"callmed-email-reply:{conversaId}"));
        // 96 bits suficientes para tornar o alias opaco; o segredo fica no servidor.
        return Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant();
    }

    private static string NormalizarDominio(string dominio) =>
        (dominio ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    private static bool DominioValido(string? dominio)
    {
        if (string.IsNullOrWhiteSpace(dominio) || dominio.Contains('@') || dominio.Contains('/') || dominio.Contains(':'))
            return false;

        var normalizado = NormalizarDominio(dominio);
        return normalizado.Contains('.') &&
            MailAddress.TryCreate("resposta@" + normalizado, out var mailbox) &&
            string.Equals(mailbox.Host, normalizado, StringComparison.OrdinalIgnoreCase);
    }
}
