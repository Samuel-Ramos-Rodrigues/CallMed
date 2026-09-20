using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Email;

namespace MKSANCrud.Services.Atendimento.Canais.Email;

public sealed class SmtpAtendimentoSender : ICanalAtendimentoSender
{
    private readonly IEmailService _email;

    public SmtpAtendimentoSender(IEmailService email)
    {
        _email = email;
    }

    public CanalAtendimento Canal => CanalAtendimento.Email;
    public bool Configurado => _email.Configurado;

    public async Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default)
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

        var enviado = await _email.EnviarAsync(
            destinatario,
            string.IsNullOrWhiteSpace(assunto)
                ? "Atendimento CallMed"
                : assunto,
            html,
            ct);

        return enviado
            ? CanalEnvioResultado.Ok()
            : CanalEnvioResultado.Falha(
                "O servidor SMTP não confirmou o envio.");
    }
}
