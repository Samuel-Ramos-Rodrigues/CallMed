using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento;

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

    public CanalAtendimento Canal =>
        CanalAtendimento.Sms;

    public bool Configurado =>
        _options.Enabled &&
        Uri.TryCreate(
            _options.BaseUrl,
            UriKind.Absolute,
            out _)
        &&
        !string.IsNullOrWhiteSpace(_options.Username) &&
        !string.IsNullOrWhiteSpace(_options.Password);

    public async Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default)
    {
        if (!Configurado)
        {
            return CanalEnvioResultado.Falha(
                "SMS Gateway não está configurado.");
        }

        var telefone =
            AtendimentoIdentidadeService.NormalizarTelefone(
                destinatario);

        if (telefone.Length < 12)
        {
            return CanalEnvioResultado.Falha(
                "Telefone inválido para envio de SMS.");
        }

        var endpoint =
            $"{_options.BaseUrl.TrimEnd('/')}/" +
            $"{_options.SendPath.TrimStart('/')}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint);

        var credenciais =
            $"{_options.Username}:{_options.Password}";

        var bytes =
            Encoding.UTF8.GetBytes(credenciais);

        var base64 =
            Convert.ToBase64String(bytes);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                base64);

        request.Content = JsonContent.Create(new
        {
            phoneNumbers = new[]
            {
                $"+{telefone}"
            },
            textMessage = new
            {
                text = texto.Trim()
            }
        });

        try
        {
            using var response =
                await _http.SendAsync(request, ct);

            var corpo =
                await response.Content
                    .ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SMS Gateway retornou HTTP {Status}.",
                    (int)response.StatusCode);

                return CanalEnvioResultado.Falha(
                    $"SMS Gateway retornou HTTP {(int)response.StatusCode}.");
            }

            var id =
                TentarExtrairId(corpo);

            _logger.LogInformation(
                "SMS enviado pelo SMS Gateway. Destinatario={Destinatario}, MessageId={MessageId}",
                MascararTelefone(telefone),
                id ?? "não informado");

            return CanalEnvioResultado.Ok(id);
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Tempo limite excedido ao enviar SMS pelo SMS Gateway.");

            return CanalEnvioResultado.Falha(
                "Tempo limite ao comunicar com o SMS Gateway.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao enviar SMS pelo SMS Gateway.");

            return CanalEnvioResultado.Falha(
                "Falha de comunicação com o SMS Gateway.");
        }
    }

    private static string? TentarExtrairId(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc =
                JsonDocument.Parse(json);

            var root = doc.RootElement;

            if (root.TryGetProperty(
                    "id",
                    out var id))
            {
                return id.ToString();
            }

            if (root.TryGetProperty(
                    "messageId",
                    out var messageId))
            {
                return messageId.ToString();
            }

            if (root.TryGetProperty(
                    "data",
                    out var data) &&
                data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty(
                        "id",
                        out var dataId))
                {
                    return dataId.ToString();
                }

                if (data.TryGetProperty(
                        "messageId",
                        out var dataMessageId))
                {
                    return dataMessageId.ToString();
                }
            }
        }
        catch
        {
            // O ID é opcional para o funcionamento do envio.
        }

        return null;
    }

    private static string MascararTelefone(
        string telefone)
    {
        if (telefone.Length <= 4)
            return "****";

        return $"****{telefone[^4..]}";
    }
}
