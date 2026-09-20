namespace MKSANCrud.Services.Email;

public interface IEmailService
{
    bool Configurado { get; }

    Task<bool> EnviarAsync(
        string destinatario,
        string assunto,
        string corpoHtml,
        CancellationToken ct = default);
}
