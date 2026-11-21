using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Atendimento.Canais.Email;

public sealed record GmailInboxMessage(
    string Id,
    string ThreadId,
    string SenderEmail,
    string Subject,
    string Body,
    long InternalDateUnixMs,
    bool IsAutomated,
    string? AutomatedReason);

/// <summary>
/// Cliente REST pequeno para Gmail API. Não armazena senha do Gmail: autentica usando OAuth refresh token.
/// As mensagens processadas recebem um marcador da própria caixa para impedir reprocessamento.
/// </summary>
public sealed class GmailApiClient
{
    private readonly HttpClient _http;
    private readonly GmailInboxOptions _options;
    private readonly ILogger<GmailApiClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly SemaphoreSlim _labelLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt;
    private string? _processedLabelId;

    public GmailApiClient(
        IHttpClientFactory clients,
        IOptions<GmailInboxOptions> options,
        ILogger<GmailApiClient> logger)
    {
        _http = clients.CreateClient("GmailApi");
        _options = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool Configurado => _options.Configurado;

    public async Task<IReadOnlyList<string>> ListarMensagensPendentesAsync(CancellationToken ct)
    {
        if (!Configurado) return Array.Empty<string>();

        var labelId = await GarantirLabelProcessadoAsync(ct);
        if (string.IsNullOrWhiteSpace(labelId)) return Array.Empty<string>();

        var max = Math.Clamp(_options.MaxMessagesPerCycle, 1, 50);
        if (!_options.TryGetStartAfterUtc(out var startAfterUtc))
            return Array.Empty<string>();

        // Gmail aceita after: em segundos Unix. O filtro temporal evita importar o histórico da caixa
        // inteira ao ativar a integração; o poller também verifica o instante exato em milissegundos.
        var afterUnixSeconds = startAfterUtc.ToUnixTimeSeconds();
        var query = $"in:inbox after:{afterUnixSeconds} -label:{_options.ProcessedLabelName} -from:{_options.MailboxEmail}";
        using var response = await EnviarGmailAsync(
            HttpMethod.Get,
            $"/gmail/v1/users/me/messages?maxResults={max}&q={Uri.EscapeDataString(query)}",
            null,
            ct);

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw CriarErroApi("listar mensagens", response.StatusCode, json);

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return messages.EnumerateArray()
            .Select(m => PropriedadeTexto(m, "id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Take(max)
            .ToArray();
    }

    public async Task<GmailInboxMessage?> ObterMensagemAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 200) return null;

        using var response = await EnviarGmailAsync(
            HttpMethod.Get,
            $"/gmail/v1/users/me/messages/{Uri.EscapeDataString(id)}?format=full",
            null,
            ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw CriarErroApi("obter mensagem", response.StatusCode, json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var messageId = PropriedadeTexto(root, "id") ?? id;
        var threadId = PropriedadeTexto(root, "threadId") ?? string.Empty;
        var headers = LerCabecalhos(root);
        var fromHeader = ValorCabecalho(headers, "From");
        if (!MailAddress.TryCreate(fromHeader, out var from) || string.IsNullOrWhiteSpace(from.Address))
            return new GmailInboxMessage(messageId, threadId, string.Empty, ValorCabecalho(headers, "Subject"), string.Empty,
                LerDataInterna(root), true, "Remetente ausente ou inválido.");

        var subject = DecodificarCabecalho(ValorCabecalho(headers, "Subject"));
        var body = ExtrairCorpo(root);
        var reason = MotivoIgnorar(headers, from.Address);
        return new GmailInboxMessage(
            messageId,
            threadId,
            from.Address.Trim(),
            Limitar(subject, 300),
            Limitar(body, 5000),
            LerDataInterna(root),
            reason is not null,
            reason);
    }

    public async Task MarcarComoProcessadaAsync(string messageId, CancellationToken ct)
    {
        var labelId = await GarantirLabelProcessadoAsync(ct);
        if (string.IsNullOrWhiteSpace(labelId))
            throw new InvalidOperationException("Não foi possível obter/criar a etiqueta do Gmail para mensagens processadas.");

        var remove = _options.MarkProcessedAsRead ? new[] { "UNREAD" } : Array.Empty<string>();
        var payload = JsonSerializer.Serialize(new { addLabelIds = new[] { labelId }, removeLabelIds = remove });
        using var response = await EnviarGmailAsync(
            HttpMethod.Post,
            $"/gmail/v1/users/me/messages/{Uri.EscapeDataString(messageId)}/modify",
            payload,
            ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw CriarErroApi("marcar mensagem como processada", response.StatusCode, json);
    }

    private async Task<string?> GarantirLabelProcessadoAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_processedLabelId)) return _processedLabelId;
        await _labelLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_processedLabelId)) return _processedLabelId;

            using var listResponse = await EnviarGmailAsync(HttpMethod.Get, "/gmail/v1/users/me/labels", null, ct);
            var listJson = await listResponse.Content.ReadAsStringAsync(ct);
            if (!listResponse.IsSuccessStatusCode)
                throw CriarErroApi("listar etiquetas", listResponse.StatusCode, listJson);

            using (var doc = JsonDocument.Parse(listJson))
            {
                if (doc.RootElement.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
                {
                    foreach (var label in labels.EnumerateArray())
                    {
                        if (string.Equals(PropriedadeTexto(label, "name"), _options.ProcessedLabelName, StringComparison.OrdinalIgnoreCase))
                        {
                            _processedLabelId = PropriedadeTexto(label, "id");
                            return _processedLabelId;
                        }
                    }
                }
            }

            var createBody = JsonSerializer.Serialize(new
            {
                name = _options.ProcessedLabelName,
                labelListVisibility = "labelShow",
                messageListVisibility = "show"
            });
            using var createResponse = await EnviarGmailAsync(HttpMethod.Post, "/gmail/v1/users/me/labels", createBody, ct);
            var createJson = await createResponse.Content.ReadAsStringAsync(ct);
            if (!createResponse.IsSuccessStatusCode)
                throw CriarErroApi("criar etiqueta de mensagens processadas", createResponse.StatusCode, createJson);
            using var created = JsonDocument.Parse(createJson);
            _processedLabelId = PropriedadeTexto(created.RootElement, "id");
            return _processedLabelId;
        }
        finally { _labelLock.Release(); }
    }

    private async Task<HttpResponseMessage> EnviarGmailAsync(HttpMethod method, string path, string? jsonBody, CancellationToken ct)
    {
        var token = await ObterAccessTokenAsync(ct);
        using var request = new HttpRequestMessage(method, "https://gmail.googleapis.com" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<string> ObterAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _accessToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return _accessToken;

            using var response = await _http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["refresh_token"] = _options.RefreshToken,
                    ["grant_type"] = "refresh_token"
                }),
                ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw CriarErroApi("renovar autorização OAuth do Gmail", response.StatusCode, json);

            using var doc = JsonDocument.Parse(json);
            _accessToken = PropriedadeTexto(doc.RootElement, "access_token");
            var expires = doc.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds)
                ? Math.Clamp(seconds, 60, 86400) : 3600;
            if (string.IsNullOrWhiteSpace(_accessToken))
                throw new InvalidOperationException("O Google não retornou access_token. Confira o refresh token OAuth.");
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expires);
            _logger.LogDebug("Token OAuth do Gmail renovado.");
            return _accessToken;
        }
        finally { _tokenLock.Release(); }
    }

    private static Dictionary<string, string> LerCabecalhos(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("payload", out var payload) ||
            !payload.TryGetProperty("headers", out var headers) || headers.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var header in headers.EnumerateArray())
        {
            var name = PropriedadeTexto(header, "name");
            var value = PropriedadeTexto(header, "value");
            if (!string.IsNullOrWhiteSpace(name) && value is not null && !result.ContainsKey(name))
                result[name] = value;
        }
        return result;
    }

    private static string ValorCabecalho(Dictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) ? value : string.Empty;

    private static string? MotivoIgnorar(Dictionary<string, string> headers, string remetente)
    {
        if (remetente.Contains("mailer-daemon", StringComparison.OrdinalIgnoreCase) ||
            remetente.Contains("postmaster", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(remetente, @"(^|[._+-])(no[-_.]?reply|donotreply)([.@_+-]|$)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
            return "Mensagem automática/de sistema.";

        var autoSubmitted = ValorCabecalho(headers, "Auto-Submitted");
        if (!string.IsNullOrWhiteSpace(autoSubmitted) && !string.Equals(autoSubmitted.Trim(), "no", StringComparison.OrdinalIgnoreCase))
            return "Cabeçalho Auto-Submitted indica resposta automática.";
        var precedence = ValorCabecalho(headers, "Precedence");
        if (precedence.Contains("bulk", StringComparison.OrdinalIgnoreCase) ||
            precedence.Contains("list", StringComparison.OrdinalIgnoreCase) ||
            precedence.Contains("junk", StringComparison.OrdinalIgnoreCase) ||
            headers.ContainsKey("List-Id") || headers.ContainsKey("List-Unsubscribe") ||
            headers.ContainsKey("X-Autoreply") || headers.ContainsKey("X-Autorespond"))
            return "Newsletter, lista ou resposta automática.";
        return null;
    }

    private static string ExtrairCorpo(JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var payload)) return string.Empty;
        var plain = new List<string>();
        var html = new List<string>();
        ColetarCorpos(payload, plain, html);
        if (plain.Count > 0) return string.Join("\n", plain).Trim();
        if (html.Count == 0) return string.Empty;
        var source = string.Join("\n", html);
        source = Regex.Replace(source, @"<(script|style)\b[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(150));
        source = Regex.Replace(source, @"<\s*br\s*/?\s*>|</\s*(p|div|li|tr)\s*>", "\n", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(150));
        source = Regex.Replace(source, @"<[^>]{0,1000}>", " ", RegexOptions.Singleline, TimeSpan.FromMilliseconds(150));
        return WebUtility.HtmlDecode(source).Replace("\0", string.Empty).Trim();
    }

    private static void ColetarCorpos(JsonElement part, List<string> plain, List<string> html)
    {
        if (PropriedadeTexto(part, "filename") is { Length: > 0 }) return; // Nunca interpretar anexos como instruções.
        var mime = PropriedadeTexto(part, "mimeType") ?? string.Empty;
        if (part.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Object &&
            body.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
        {
            var decoded = DecodificarBase64Url(data.GetString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                if (mime.Equals("text/plain", StringComparison.OrdinalIgnoreCase)) plain.Add(decoded);
                else if (mime.Equals("text/html", StringComparison.OrdinalIgnoreCase)) html.Add(decoded);
            }
        }
        if (part.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            foreach (var child in parts.EnumerateArray()) ColetarCorpos(child, plain, html);
    }

    private static string DecodificarBase64Url(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(base64)); }
        catch (FormatException) { return string.Empty; }
    }

    private static string DecodificarCabecalho(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("=?", StringComparison.Ordinal)) return value;
        return Regex.Replace(value, @"=\?(?<charset>[^?]+)\?(?<encoding>[bBqQ])\?(?<data>[^?]*)\?=", match =>
        {
            try
            {
                var charset = Encoding.GetEncoding(match.Groups["charset"].Value);
                var data = match.Groups["data"].Value;
                var bytes = match.Groups["encoding"].Value.Equals("B", StringComparison.OrdinalIgnoreCase)
                    ? Convert.FromBase64String(data)
                    : DecodificarQuotedPrintableCabecalho(data);
                return charset.GetString(bytes);
            }
            catch { return match.Value; }
        }, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(150));
    }

    private static byte[] DecodificarQuotedPrintableCabecalho(string value)
    {
        using var output = new MemoryStream();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '_' ) { output.WriteByte((byte)' '); continue; }
            if (value[i] == '=' && i + 2 < value.Length &&
                byte.TryParse(value.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            { output.WriteByte(b); i += 2; continue; }
            output.WriteByte((byte)value[i]);
        }
        return output.ToArray();
    }

    private static long LerDataInterna(JsonElement root) =>
        long.TryParse(PropriedadeTexto(root, "internalDate"), out var value) ? value : 0;

    private static string? PropriedadeTexto(JsonElement obj, string property)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(property, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static string Limitar(string? value, int max)
    {
        var clean = value?.Trim().Replace("\0", string.Empty) ?? string.Empty;
        return clean[..Math.Min(clean.Length, max)];
    }

    private static HttpRequestException CriarErroApi(string acao, HttpStatusCode status, string corpo)
    {
        // Não logamos payload, cabeçalhos de autorização nem conteúdo do e-mail.
        var detalhe = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            detalhe = PropriedadeTexto(doc.RootElement, "error_description") ??
                      (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : null) ?? string.Empty;
        }
        catch { }
        if (detalhe.Length > 180) detalhe = detalhe[..180];
        return new HttpRequestException($"Falha ao {acao} no Gmail API (HTTP {(int)status}). {detalhe}".Trim(), null, status);
    }
}
