using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Email;

/// <summary>Envia e-mails transacionais pelo transporte SMTP autenticado configurado (Brevo).</summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(_options.Host) &&
        _options.Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(_options.Username) &&
        !string.IsNullOrWhiteSpace(_options.Password) &&
        MailAddress.TryCreate(_options.FromEmail, out _);

    public async Task<bool> EnviarAsync(
        string destinatario,
        string assunto,
        string corpoHtml,
        CancellationToken ct = default)
    {
        if (!Configurado)
        {
            _logger.LogWarning(
                "O transporte SMTP não está configurado. Verifique host, porta, usuário SMTP, senha SMTP e remetente verificado.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(destinatario) ||
            !MailAddress.TryCreate(destinatario.Trim(), out var destinatarioValidado) ||
            !string.Equals(destinatarioValidado.Address, destinatario.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Tentativa de envio SMTP com endereço destinatário inválido.");
            return false;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            using var mensagem = new MailMessage
            {
                From = new MailAddress(_options.FromEmail.Trim(), _options.FromName ?? "CallMed"),
                Subject = assunto ?? string.Empty,
                Body = corpoHtml ?? string.Empty,
                IsBodyHtml = true,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding = System.Text.Encoding.UTF8,
                HeadersEncoding = System.Text.Encoding.UTF8
            };
            mensagem.To.Add(destinatarioValidado);

            using var smtp = new SmtpClient(_options.Host.Trim(), _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_options.Username, _options.Password),
                Timeout = 30000
            };

            // Porta 2525 com EnableSsl=true usa STARTTLS no Brevo.
            await smtp.SendMailAsync(mensagem, ct);

            _logger.LogInformation("E-mail aceito pelo servidor SMTP para processamento.");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                "Falha no envio SMTP. Código SMTP={SmtpStatusCode}; tipo={ExceptionType}.",
                ex.StatusCode,
                ex.GetType().Name);
            return false;
        }
        catch (Exception ex)
        {
            // Não registrar destinatário, assunto, conteúdo ou credenciais.
            _logger.LogError(
                "Falha no envio SMTP. Tipo={ExceptionType}.",
                ex.GetType().Name);
            return false;
        }
    }
}
