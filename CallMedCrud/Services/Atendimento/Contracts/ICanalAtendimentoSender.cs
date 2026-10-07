using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento;

public interface ICanalAtendimentoSender
{
    CanalAtendimento Canal { get; }
    bool Configurado { get; }

    Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default);
}
