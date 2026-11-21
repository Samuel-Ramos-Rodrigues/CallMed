using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace MKSANCrud.Services.Atendimento;

/// <summary>
/// Valida o segredo dos webhooks por cabeçalho HTTP ou query string.
/// Prioriza os cabeçalhos e utiliza a query string como alternativa.
/// </summary>
public static class AtendimentoWebhookSecretValidator
{
    public static bool Validar(HttpRequest request, string? configurado)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(configurado))
            return false;

        // Primeiro, tenta obter o segredo pelo cabeçalho CallMed.
        var recebido = request.Headers["X-CallMed-Webhook-Secret"].ToString();

        // Se estiver vazio, tenta o cabeçalho MKSAN.
        if (string.IsNullOrWhiteSpace(recebido))
            recebido = request.Headers["X-MKSAN-Webhook-Secret"].ToString();

        // Se os cabeçalhos não fornecerem o segredo, tenta a query string.
        if (string.IsNullOrWhiteSpace(recebido))
            recebido = request.Query["secret"].ToString();

        if (string.IsNullOrWhiteSpace(recebido))
            return false;

        // Converte os segredos para bytes.
        var esperado = Encoding.UTF8.GetBytes(configurado);
        var informado = Encoding.UTF8.GetBytes(recebido);

        // Compara os valores de forma segura.
        return esperado.Length == informado.Length &&
               CryptographicOperations.FixedTimeEquals(esperado, informado);
    }
}
