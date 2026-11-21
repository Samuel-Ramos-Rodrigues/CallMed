using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MKSANCrud.Services.Atendimento.Canais.Sms;

public static class HttpSmsWebhookValidator
{
    public static bool Validar(string authorization, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) ||
            !AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter) || header.Parameter.Length > 16384) return false;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 16384 };
            handler.ValidateToken(header.Parameter, new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                // httpSMS não documenta issuer/audience obrigatórios. Expiração e
                // not-before são validados quando presentes, além da assinatura.
                ValidateIssuer = false, ValidateAudience = false,
                ValidateLifetime = true, RequireExpirationTime = false,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            return true;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return false; }
    }
}
