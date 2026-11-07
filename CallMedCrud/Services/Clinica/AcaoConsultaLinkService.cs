using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Clinica;

// Links assinados e com validade, para agir sobre uma consulta ou vaga sem login.
// O GET só mostra a situação; a alteração acontece num POST com antiforgery.
// Formato do token protegido: id|acao|expiraEmUnixSegundos|extra
public sealed class AcaoConsultaLinkService(
    IDataProtectionProvider protection,
    IOptions<AtendimentoEmailOptions> options)
{
    public readonly record struct Link(int Id, string Acao, int Extra);

    private static readonly string[] AcoesValidas = ["confirmar", "cancelar", "preencher", "aceitar", "reagendar", "sala"];
    private static readonly TimeSpan Validade = TimeSpan.FromHours(36);
    private readonly IDataProtector _protector = protection.CreateProtector("CallMed.AcaoConsulta.v1");

    // Retorna null quando PortalBaseUrl não é uma URL https válida; a mensagem então segue sem o link.
    public string? GerarUrl(int id, string acao, int extra = 0)
    {
        if (!AcoesValidas.Contains(acao)) return null;
        var expira = DateTimeOffset.UtcNow.Add(Validade).ToUnixTimeSeconds();
        var token = _protector.Protect($"{id}|{acao}|{expira}|{extra}");
        return options.Value.UrlPortal($"consulta/acao/{Uri.EscapeDataString(token)}");
    }

    public bool TryLer(string token, out Link link)
    {
        link = default;
        try
        {
            var partes = _protector.Unprotect(token).Split('|');
            if (partes.Length != 4 ||
                !int.TryParse(partes[0], out var id) ||
                !AcoesValidas.Contains(partes[1]) ||
                !long.TryParse(partes[2], out var expira) ||
                !int.TryParse(partes[3], out var extra) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira)
                return false;

            link = new Link(id, partes[1], extra);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }
}
