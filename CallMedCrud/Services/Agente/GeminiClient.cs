using MKSANCrud.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MKSANCrud.Services.Agente;

public sealed class GeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.Timeout =
            TimeSpan.FromSeconds(
                Math.Clamp(
                    _options.TimeoutSeconds,
                    10,
                    180));
    }

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(
            _options.ApiKey);

    public string Model
    {
        get
        {
            var model =
                string.IsNullOrWhiteSpace(
                    _options.Model)
                    ? "gemini-3.1-flash-lite"
                    : _options.Model.Trim();

            return model.StartsWith(
                "models/",
                StringComparison.OrdinalIgnoreCase)
                ? model["models/".Length..]
                : model;
        }
    }

    public async Task<JsonObject> GerarAsync(
        JsonObject request,
        CancellationToken cancellationToken)
    {
        if (!Configurado)
            throw new InvalidOperationException(
                "Gemini:ApiKey não configurada.");

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/" +
            $"{Uri.EscapeDataString(Model)}:generateContent";

        const int maxTentativas = 3;

        for (var tentativa = 1;
             tentativa <= maxTentativas;
             tentativa++)
        {
            using var message =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    url)
                {
                    Content =
                        JsonContent.Create(request)
                };

            message.Headers.Add(
                "x-goog-api-key",
                _options.ApiKey);

            try
            {
                using var response =
                    await _httpClient.SendAsync(
                        message,
                        cancellationToken);

                var raw =
                    await response.Content
                        .ReadAsStringAsync(
                            cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var node =
                        JsonNode.Parse(raw)
                        as JsonObject;

                    if (node is null)
                        throw new JsonException(
                            "Resposta do Gemini não é um objeto JSON válido.");

                    return node;
                }

                var transitorio =
                    response.StatusCode ==
                        HttpStatusCode.TooManyRequests ||
                    response.StatusCode ==
                        HttpStatusCode.InternalServerError ||
                    response.StatusCode ==
                        HttpStatusCode.BadGateway ||
                    response.StatusCode ==
                        HttpStatusCode.ServiceUnavailable ||
                    response.StatusCode ==
                        HttpStatusCode.GatewayTimeout;

                _logger.LogWarning(
                    "Gemini respondeu HTTP {StatusCode}. Tentativa {Tentativa}/{Max}. Corpo resumido: {Body}",
                    (int)response.StatusCode,
                    tentativa,
                    maxTentativas,
                    Limitar(raw));

                if (!transitorio ||
                    tentativa == maxTentativas)
                {
                    throw new HttpRequestException(
                        $"Gemini retornou HTTP {(int)response.StatusCode}.",
                        null,
                        response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
                when (tentativa < maxTentativas &&
                      ex.StatusCode is null)
            {
                _logger.LogWarning(
                    ex,
                    "Falha transitória de rede com Gemini. Tentativa {Tentativa}/{Max}.",
                    tentativa,
                    maxTentativas);
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(
                    450 * tentativa),
                cancellationToken);
        }

        throw new HttpRequestException(
            "Não foi possível obter resposta do Gemini após novas tentativas.");
    }

    private static string Limitar(
        string valor)
        => valor.Length <= 1200
            ? valor
            : valor[..1200] + "...";
}
