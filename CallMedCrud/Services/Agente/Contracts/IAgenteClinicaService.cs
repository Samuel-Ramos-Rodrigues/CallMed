using MKSANCrud.DTOs.Agente;

namespace MKSANCrud.Services.Agente;

public interface IAgenteClinicaService
{
    Task<AgenteResposta> EnviarAsync(
        string mensagem,
        string? sessionId,
        AgenteUsuarioContexto usuario,
        IReadOnlyList<MensagemHistoricoAgente>? historicoCliente = null,
        CancellationToken cancellationToken = default);
}
