using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoEnvioService
{
    private readonly IReadOnlyDictionary<CanalAtendimento, ICanalAtendimentoSender> _senders;
    private readonly AtendimentoConversaService _conversas;
    private readonly ILogger<AtendimentoEnvioService> _logger;

    public AtendimentoEnvioService(
        IEnumerable<ICanalAtendimentoSender> senders,
        AtendimentoConversaService conversas,
        ILogger<AtendimentoEnvioService> logger)
    {
        _senders = senders.ToDictionary(s => s.Canal);
        _conversas = conversas;
        _logger = logger;
    }

    public bool CanalConfigurado(CanalAtendimento canal) =>
        canal == CanalAtendimento.Web ||
        (_senders.TryGetValue(canal, out var sender) &&
         sender.Configurado);

    public async Task<MensagemAtendimento> EnviarAsync(
        ConversaAtendimento conversa,
        string texto,
        AutorMensagemAtendimento autor,
        string? autorUsuarioId = null,
        string? assunto = null,
        CancellationToken ct = default)
    {
        if (conversa.Canal == CanalAtendimento.Web)
        {
            return await _conversas.RegistrarSaidaAsync(
                conversa,
                texto,
                autor,
                StatusMensagemAtendimento.Enviada,
                autorUsuarioId,
                ct: ct);
        }

        if (!_senders.TryGetValue(conversa.Canal, out var sender))
        {
            return await _conversas.RegistrarSaidaAsync(
                conversa,
                texto,
                autor,
                StatusMensagemAtendimento.Falhou,
                autorUsuarioId,
                erro: "Canal sem provedor registrado.",
                ct: ct);
        }

        if (!sender.Configurado)
        {
            return await _conversas.RegistrarSaidaAsync(
                conversa,
                texto,
                autor,
                StatusMensagemAtendimento.Falhou,
                autorUsuarioId,
                erro: "Canal ainda não foi configurado.",
                ct: ct);
        }

        CanalEnvioResultado resultado;

        try
        {
            resultado = await sender.EnviarAsync(
                conversa.IdentificadorExterno,
                texto,
                assunto ?? conversa.Assunto,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha de envio no canal {Canal} para conversa {ConversaId}.",
                conversa.Canal,
                conversa.Id);

            resultado = CanalEnvioResultado.Falha(
                "Falha inesperada ao chamar o provedor.");
        }

        return await _conversas.RegistrarSaidaAsync(
            conversa,
            texto,
            autor,
            resultado.Sucesso
                ? StatusMensagemAtendimento.Enviada
                : StatusMensagemAtendimento.Falhou,
            autorUsuarioId,
            resultado.MensagemExternaId,
            resultado.Erro,
            ct);
    }
}
