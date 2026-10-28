using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace MKSANCrud.Services.Atendimento;

/// <summary>
/// Valida o segredo dos webhooks usando somente cabeçalhos HTTP.
/// A query string é deliberadamente ignorada para evitar vazamento em logs e proxies.
/// </summary>
public static class AtendimentoWebhookSecretValidator
{
    public static bool Validar(HttpRequest request, string? configurado)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(configurado))
            return false;

        var recebido = request.Headers["X-CallMed-Webhook-Secret"].ToString();
        if (string.IsNullOrWhiteSpace(recebido))
            recebido = request.Headers["X-MKSAN-Webhook-Secret"].ToString();

        if (string.IsNullOrWhiteSpace(recebido))
            return false;

        var esperado = Encoding.UTF8.GetBytes(configurado);
        var informado = Encoding.UTF8.GetBytes(recebido);

        return esperado.Length == informado.Length &&
               CryptographicOperations.FixedTimeEquals(esperado, informado);
    }
}
