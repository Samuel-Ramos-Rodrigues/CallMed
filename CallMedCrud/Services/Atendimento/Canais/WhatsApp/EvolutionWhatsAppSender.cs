using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento.Canais.WhatsApp;

public sealed class EvolutionWhatsAppSender : ICanalAtendimentoSender
{
    private readonly HttpClient _http;
    private readonly EvolutionWhatsAppOptions _options;
    private readonly ILogger<EvolutionWhatsAppSender> _logger;

    public EvolutionWhatsAppSender(
        HttpClient http,
        IOptions<EvolutionWhatsAppOptions> options,
        ILogger<EvolutionWhatsAppSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public CanalAtendimento Canal => CanalAtendimento.WhatsApp;

    public bool Configurado =>
        _options.Enabled &&
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.InstanceName);

    public async Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default)
    {
        if (!Configurado)
            return CanalEnvioResultado.Falha("Evolution API não configurada.");

        var numero = AtendimentoIdentidadeService.NormalizarTelefone(destinatario);

        if (string.IsNullOrWhiteSpace(numero))
            return CanalEnvioResultado.Falha("Telefone inválido.");

        string? ultimoId = null;

        foreach (var parte in DividirMensagem(texto.Trim(), 3500))
        {
            var endpoint =
                $"{_options.BaseUrl.TrimEnd('/')}/message/sendText/{Uri.EscapeDataString(_options.InstanceName)}";

            try
            {
                // Evolution atual: { number, text }.
                var tentativaAtual = await EnviarParteAsync(
                    endpoint,
                    numero,
                    parte,
                    payloadLegado: false,
                    ct);

                // Algumas instalações antigas ainda esperam { number, textMessage: { text } }.
                // Só há fallback em erro de validação, evitando duplicar mensagem aceita.
                if (!tentativaAtual.Sucesso && tentativaAtual.PermiteFallbackLegado)
                {
                    _logger.LogInformation(
                        "Evolution rejeitou payload atual; tentando formato legado compatível.");

                    tentativaAtual = await EnviarParteAsync(
                        endpoint,
                        numero,
                        parte,
                        payloadLegado: true,
                        ct);
                }

                if (!tentativaAtual.Sucesso)
                {
                    _logger.LogWarning(
                        "Evolution API retornou HTTP {Status} no envio.",
                        (int)tentativaAtual.StatusCode);

                    return CanalEnvioResultado.Falha(
                        $"Evolution retornou HTTP {(int)tentativaAtual.StatusCode}.");
                }

                ultimoId ??= TentarExtrairId(tentativaAtual.Corpo);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao enviar mensagem via Evolution API.");
                return CanalEnvioResultado.Falha("Falha de comunicação com a Evolution API.");
            }
        }

        return CanalEnvioResultado.Ok(ultimoId);
    }

    private async Task<TentativaEnvio> EnviarParteAsync(
        string endpoint,
        string numero,
        string texto,
        bool payloadLegado,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("apikey", _options.ApiKey);

        request.Content = payloadLegado
            ? JsonContent.Create(new
            {
                number = numero,
                textMessage = new { text = texto }
            })
            : JsonContent.Create(new
            {
                number = numero,
                text = texto
            });

        using var response = await _http.SendAsync(request, ct);
        var corpo = await response.Content.ReadAsStringAsync(ct);

        return new TentativaEnvio(
            response.IsSuccessStatusCode,
            response.StatusCode,
            corpo,
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity);
    }

    private static string? TentarExtrairId(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("key", out var key) &&
                key.ValueKind == JsonValueKind.Object &&
                key.TryGetProperty("id", out var id) &&
                id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }

            if (root.TryGetProperty("id", out var id2) &&
                id2.ValueKind == JsonValueKind.String)
            {
                return id2.GetString();
            }
        }
        catch
        {
            // O envio já foi confirmado pelo HTTP; o ID é opcional.
        }

        return null;
    }

    private static IEnumerable<string> DividirMensagem(string texto, int max)
    {
        if (texto.Length <= max)
        {
            yield return texto;
            yield break;
        }

        var restante = texto;

        while (restante.Length > max)
        {
            var corte = restante.LastIndexOf('\n', max);

            if (corte < max / 2)
                corte = restante.LastIndexOf(' ', max);

            if (corte < max / 2)
                corte = max;

            yield return restante[..corte].Trim();
            restante = restante[corte..].TrimStart();
        }

        if (restante.Length > 0)
            yield return restante;
    }

    private sealed record TentativaEnvio(
        bool Sucesso,
        HttpStatusCode StatusCode,
        string Corpo,
        bool PermiteFallbackLegado);
}
