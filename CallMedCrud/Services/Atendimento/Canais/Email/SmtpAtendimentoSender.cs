using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Email;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;
using System.Text.RegularExpressions;
using System.Net;

namespace MKSANCrud.Services.Atendimento.Canais.Email;

public sealed class SmtpAtendimentoSender : ICanalAtendimentoSender
{
    private readonly IEmailService _email;
    private readonly AtendimentoEmailOptions _options;

    public SmtpAtendimentoSender(IEmailService email, IOptions<AtendimentoEmailOptions>? options = null)
    {
        _email = email;
        _options = options?.Value ?? new();
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

        var html =
            "<div style=\"font-family:Arial,sans-serif;line-height:1.55\">" +
            "<h2 style=\"color:#0b8f55\">CallMed</h2>" +
            string.Join(
                "<br>",
                System.Net.WebUtility.HtmlEncode(texto)
                    .Replace("\r\n", "\n")
                    .Split('\n')) +
            "</div>";

        var links = new[] { _options.UrlPortal("Consulta/Create"), _options.UrlPortal("Consulta") }
            .Where(x => x is not null).Cast<string>().Select(WebUtility.HtmlEncode).ToArray();
        if (links.Length > 0)
            html = Regex.Replace(html, "(?:" + string.Join("|", links.Select(x => Regex.Escape(x!))) + @")(?:\?[^\s<>]*)?",
                match =>
                {
                    var url = match.Value.TrimEnd('.', ')', ']');
                    return $"<a href=\"{url}\">{url}</a>" + match.Value[url.Length..];
                }, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

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
