using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Email;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;
using System.Text.RegularExpressions;
using System.Net;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Services.Atendimento.Canais.Email;

public sealed class SmtpAtendimentoSender : ICanalAtendimentoSender
{
    private readonly IEmailService _email;
    private readonly AtendimentoEmailOptions _options;
    private readonly Uri? _publicOrigin;

    public SmtpAtendimentoSender(IEmailService email, IOptions<AtendimentoEmailOptions>? options = null, IConfiguration? config = null)
    {
        _email = email;
        _options = options?.Value ?? new();
        _publicOrigin = config is null ? null : PublicOrigin.TentarObter(config);
    }

    public CanalAtendimento Canal => CanalAtendimento.Email;
    public bool Configurado => _email.Configurado;

    public async Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default,
        string? replyTo = null)
    {
        if (!_email.Configurado)
            return CanalEnvioResultado.Falha(
                "SMTP não configurado.");

        // Escapa todo o texto antes de criar anchors, evitando HTML injetado nos avisos.
        // Antes eram clicáveis apenas /Consulta e /Consulta/Create: o link de lembrete
        // /consulta-acao?token=... era enviado como texto simples dentro do HTML.
        var htmlBody = new System.Text.StringBuilder();
        var portal = _options.UrlPortal("Consulta");
        var origin = _publicOrigin?.GetLeftPart(UriPartial.Authority)
            ?? (portal is null ? null : new Uri(portal).GetLeftPart(UriPartial.Authority));
        var pattern = origin is null ? null : Regex.Escape(origin) + @"/[^\s<>""']+";
        var pos = 0;
        if (pattern is not null)
        {
            foreach (Match match in Regex.Matches(texto, pattern,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
            {
                htmlBody.Append(WebUtility.HtmlEncode(texto[pos..match.Index]));
                var url = match.Value.TrimEnd('.', ',', ';', ')', ']', '}');
                if (Uri.TryCreate(url, UriKind.Absolute, out var valid) &&
                    valid.Scheme == Uri.UriSchemeHttps && valid.UserInfo.Length == 0 &&
                    string.Equals(valid.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase))
                {
                    var encoded = WebUtility.HtmlEncode(url);
                    htmlBody.Append("<a href=\"").Append(encoded)
                        .Append("\" style=\"color:#087d52;text-decoration:underline\">")
                        .Append(encoded).Append("</a>");
                }
                else htmlBody.Append(WebUtility.HtmlEncode(url));
                htmlBody.Append(WebUtility.HtmlEncode(match.Value[url.Length..]));
                pos = match.Index + match.Length;
            }
        }
        htmlBody.Append(WebUtility.HtmlEncode(texto[pos..]));
        var html = "<div style=\"font-family:Arial,sans-serif;line-height:1.55\">" +
            "<h2 style=\"color:#0b8f55\">CallMed</h2>" +
            htmlBody.ToString().Replace("\r\n", "\n").Replace("\n", "<br>") +
            "</div>";

        var enviado = await _email.EnviarAsync(
            destinatario,
            string.IsNullOrWhiteSpace(assunto)
                ? "Atendimento CallMed"
                : assunto,
            html,
            ct,
            replyTo);

        return enviado
            ? CanalEnvioResultado.Ok()
            : CanalEnvioResultado.Falha(
                "O servidor SMTP não confirmou o envio.");
    }
}
