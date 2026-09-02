using MKSANCrud.Options;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MKSANCrud.Services.Email;

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
        !string.IsNullOrWhiteSpace(_options.FromEmail);

    public async Task<bool> EnviarAsync(
        string destinatario,
        string assunto,
        string corpoHtml,
        CancellationToken ct = default)
    {
        if (!Configurado)
            return false;

        try
        {
            using var mensagem = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = assunto,
                Body = corpoHtml,
                IsBodyHtml = true
            };

            mensagem.To.Add(destinatario);

            using var smtp = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                smtp.Credentials = new NetworkCredential(
                    _options.Username,
                    _options.Password);
            }

            ct.ThrowIfCancellationRequested();
            await smtp.SendMailAsync(mensagem);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail para {Destinatario}.", destinatario);
            return false;
        }
    }
}
