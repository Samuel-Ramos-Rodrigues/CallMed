using System.Security.Cryptography;
using System.Text;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

// Sala virtual por consulta. O nome da sala sai de HMAC com Telemedicina:Segredo,
// então não pode ser adivinhado. Sem segredo configurado, a telemedicina fica indisponível.
// Recomenda-se apontar Telemedicina:BaseUrl para um Jitsi Meet autohospedado (dados de saúde).
public sealed class TelemedicinaService(IConfiguration configuration)
{
    private const string BaseUrlPadrao = "https://meet.jit.si";

    public string? UrlSala(Consulta consulta)
    {
        var segredo = configuration["Telemedicina:Segredo"]?.Trim();
        if (string.IsNullOrWhiteSpace(segredo)) return null;

        var baseUrl = configuration["Telemedicina:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = BaseUrlPadrao;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origem) ||
            origem.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(origem.UserInfo))
            return null;

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes($"consulta:{consulta.Id}"));
        return $"{origem.GetLeftPart(UriPartial.Authority)}/CallMed-{Convert.ToHexString(hash, 0, 12).ToLowerInvariant()}";
    }

    // A sala abre 15 minutos antes e fecha 2 horas depois do horário.
    public bool JanelaAberta(Consulta consulta, DateTime agora)
    {
        if (!TimeSpan.TryParse(consulta.Horario, out var horario)) return false;
        var inicio = consulta.Data.Date.Add(horario);
        return agora >= inicio.AddMinutes(-15) && agora <= inicio.AddHours(2);
    }
}
