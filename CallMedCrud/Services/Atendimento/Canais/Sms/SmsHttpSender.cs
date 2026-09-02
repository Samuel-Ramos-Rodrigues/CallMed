using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Options;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento.Canais.Sms;

public sealed class SmsHttpSender : ICanalAtendimentoSender
{
    private readonly HttpClient _http;
    private readonly SmsHttpOptions _options;
    private readonly ILogger<SmsHttpSender> _logger;

    public SmsHttpSender(
        HttpClient http,
        IOptions<SmsHttpOptions> options,
        ILogger<SmsHttpSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public CanalAtendimento Canal => CanalAtendimento.Sms;

    public bool Configurado =>
        _options.Enabled &&
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _);

    public async Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default)
    {
        if (!Configurado)
            return CanalEnvioResultado.Falha(
                "Gateway de SMS não configurado.");

        var telefone =
            AtendimentoIdentidadeService.NormalizarTelefone(
                destinatario);

        if (telefone.Length < 10)
            return CanalEnvioResultado.Falha(
                "Telefone inválido.");

        var endpoint =
            $"{_options.BaseUrl.TrimEnd('/')}/{_options.SendPath.TrimStart('/')}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var valor = string.IsNullOrWhiteSpace(
                _options.ApiKeyScheme)
                ? _options.ApiKey
                : $"{_options.ApiKeyScheme.Trim()} {_options.ApiKey}";

            request.Headers.TryAddWithoutValidation(
                _options.ApiKeyHeader,
                valor);
        }

        request.Content = JsonContent.Create(new
        {
            to = telefone,
            from = _options.Sender,
            text = texto.Trim()
        });

        try
        {
            using var response = await _http.SendAsync(
                request,
                ct);

            var corpo = await response.Content
                .ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gateway SMS retornou HTTP {Status} no envio.",
                    (int)response.StatusCode);

                return CanalEnvioResultado.Falha(
                    $"Gateway SMS retornou HTTP {(int)response.StatusCode}.");
            }

            return CanalEnvioResultado.Ok(
                TentarExtrairId(corpo));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao enviar SMS.");

            return CanalEnvioResultado.Falha(
                "Falha de comunicação com o gateway SMS.");
        }
    }

    private static string? TentarExtrairId(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            foreach (var nome in new[]
                     {
                         "id",
                         "messageId",
                         "message_id"
                     })
            {
                if (root.TryGetProperty(nome, out var value))
                    return value.ToString();
            }
        }
        catch
        {
            // ID opcional.
        }

        return null;
    }
}
