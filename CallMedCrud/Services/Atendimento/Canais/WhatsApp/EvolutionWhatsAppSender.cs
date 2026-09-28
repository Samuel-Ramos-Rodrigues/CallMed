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
    private readonly EvolutionEnvioControle _controle;
    private readonly HttpClient _http;
    private readonly EvolutionWhatsAppOptions _options;
    private readonly ILogger<EvolutionWhatsAppSender> _logger;

    public EvolutionWhatsAppSender(
        HttpClient http,
        IOptions<EvolutionWhatsAppOptions> options,
        ILogger<EvolutionWhatsAppSender> logger,
        EvolutionEnvioControle controle)
    {
        _controle = controle;
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public CanalAtendimento Canal => CanalAtendimento.WhatsApp;

    public bool Configurado =>
        _options.Enabled &&
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var url) &&
        (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.InstanceName);

    public async Task<(bool Conectado, string Mensagem)> VerificarConexaoAsync(CancellationToken ct = default)
    {
        if (!Configurado) return (false, "Configure URL, chave e nome da instância da Evolution no servidor.");
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.BaseUrl.TrimEnd('/')}/instance/connectionState/{Uri.EscapeDataString(_options.InstanceName)}");
        request.Headers.TryAddWithoutValidation("apikey", _options.ApiKey);
        try
        {
            using var response = await _http.SendAsync(request, limite.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return (false, "Evolution limitou as consultas (HTTP 429). Aguarde antes de verificar novamente.");
            if (!response.IsSuccessStatusCode)
                return (false, $"Evolution respondeu HTTP {(int)response.StatusCode}. Confira a chave e a instância.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(limite.Token));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("instance", out var instance) || instance.ValueKind != JsonValueKind.Object ||
                !instance.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String)
                return (false, "Evolution respondeu, mas o estado da instância não foi reconhecido.");
            return string.Equals(state.GetString(), "open", StringComparison.OrdinalIgnoreCase)
                ? (true, "WhatsApp conectado à instância. O recebimento do webhook ainda deve ser testado.")
                : (false, "A instância está desconectada ou conectando. Confira o QR Code no painel da Evolution.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, "Evolution não respondeu em até 8 segundos.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return (false, "Não foi possível consultar a Evolution. Confira o endereço e a rede do servidor.");
        }
    }

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

        if (string.IsNullOrWhiteSpace(texto))
            return CanalEnvioResultado.Falha("A mensagem está vazia.");

        await _controle.Fila.WaitAsync(ct);
        try
        {
            if (_controle.BloqueadoAte is DateTime ate)
                return LimiteDeEnvio(ate);

            string? ultimoId = null;
            var partesEnviadas = 0;
            foreach (var parte in DividirMensagem(texto.Trim(), 3500))
            {
                var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/message/sendText/{Uri.EscapeDataString(_options.InstanceName)}";
                TentativaEnvio tentativa;
                try
                {
                    tentativa = await EnviarParteAsync(endpoint, numero, parte, false, ct);
                    if (!tentativa.Sucesso && tentativa.PermiteFallbackLegado)
                        tentativa = await EnviarParteAsync(endpoint, numero, parte, true, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Evolution não confirmou o resultado do envio.");
                    return new CanalEnvioResultado
                    {
                        Erro = "Envio sem confirmação da Evolution. Confira a conversa no WhatsApp antes de enviar outra mensagem.",
                        ReenvioBloqueado = true
                    };
                }

                if (!tentativa.Sucesso)
                {
                    _logger.LogWarning("Evolution API retornou HTTP {Status} no envio.", (int)tentativa.StatusCode);
                    if (partesEnviadas > 0)
                        return new CanalEnvioResultado
                        {
                            Erro = $"Envio parcial: {partesEnviadas} parte(s) confirmada(s), seguido de HTTP {(int)tentativa.StatusCode}. Confira o WhatsApp antes de enviar o trecho restante.",
                            ReenvioBloqueado = true
                        };
                    return tentativa.ProximaTentativaEm is DateTime quando
                        ? LimiteDeEnvio(quando)
                        : CanalEnvioResultado.Falha($"Evolution retornou HTTP {(int)tentativa.StatusCode}. Confira a instância e a configuração do provedor.");
                }
                _controle.RegistrarSucesso();
                partesEnviadas++;
                ultimoId ??= TentarExtrairId(tentativa.Corpo);
            }
            return CanalEnvioResultado.Ok(ultimoId);
        }
        finally { _controle.Fila.Release(); }
    }

    private static CanalEnvioResultado LimiteDeEnvio(DateTime ate) => new()
    {
        Erro = "Limite temporário da Evolution (HTTP 429). Aguarde o horário indicado para tentar novamente.",
        ProximaTentativaEm = ate
    };

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

        await _controle.AguardarIntervaloAsync(ct);
        using var response = await _http.SendAsync(request, ct);
        DateTime? proximaTentativa = response.StatusCode == HttpStatusCode.TooManyRequests
            ? _controle.RegistrarLimite(response.Headers.RetryAfter)
            : null;
        if (proximaTentativa.HasValue)
            return new TentativaEnvio(false, response.StatusCode, string.Empty, false, proximaTentativa);
        var corpo = await response.Content.ReadAsStringAsync(ct);

        return new TentativaEnvio(
            response.IsSuccessStatusCode,
            response.StatusCode,
            corpo,
            PermiteFallbackLegado(response.StatusCode, corpo),
            proximaTentativa);
    }


    private static bool PermiteFallbackLegado(
        HttpStatusCode statusCode,
        string corpo)
    {
        if (statusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            return true;

        if (statusCode != HttpStatusCode.InternalServerError ||
            string.IsNullOrWhiteSpace(corpo))
            return false;

        var pistas = new[]
        {
            "textmessage",
            "cannot read properties of undefined",
            "undefined",
            "payload",
            "validation",
            "required"
        };

        return pistas.Any(p =>
            corpo.Contains(p, StringComparison.OrdinalIgnoreCase));
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
        bool PermiteFallbackLegado,
        DateTime? ProximaTentativaEm);
}
