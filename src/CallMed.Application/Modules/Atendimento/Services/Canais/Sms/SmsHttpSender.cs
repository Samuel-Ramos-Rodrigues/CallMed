using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Atendimento.Canais.Sms;

public sealed class SmsHttpSender(
    HttpClient http, IOptions<SmsHttpOptions> options, ILogger<SmsHttpSender> logger) : ICanalAtendimentoSender
{
    private readonly SmsHttpOptions _options = options.Value;
    public CanalAtendimento Canal => CanalAtendimento.Sms;
    public bool Configurado => TentarObterConfiguracao(out _, out _);

    private bool TentarObterConfiguracao(out string endpoint, out string remetente)
    {
        endpoint = string.Empty;
        remetente = AtendimentoIdentidadeService.NormalizarTelefone(_options.From ?? string.Empty);
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || remetente.Length is < 12 or > 15 ||
            !Uri.TryCreate(_options.BaseUrl?.Trim(), UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0)
            return false;
        var caminho = _options.SendPath?.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(caminho) || caminho.Contains(':') || caminho.Contains('?') || caminho.Contains('#') ||
            caminho.Split('/').Any(p => p is "." or "..")) return false;
        endpoint = $"{url.AbsoluteUri.TrimEnd('/')}/{caminho}";
        return true;
    }

    public async Task<CanalEnvioResultado> EnviarAsync(string destinatario, string texto,
        string? assunto = null, CancellationToken ct = default, string? replyTo = null)
    {
        if (!TentarObterConfiguracao(out var endpoint, out var remetente))
            return CanalEnvioResultado.Falha("Configure URL, API Key e número do chip (From) do httpSMS no servidor.");
        var telefone = AtendimentoIdentidadeService.NormalizarTelefone(destinatario);
        if (telefone.Length is < 12 or > 15) return CanalEnvioResultado.Falha("Telefone inválido para envio de SMS.");
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > 5000)
            return CanalEnvioResultado.Falha("A mensagem deve ter entre 1 e 5000 caracteres.");

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-api-key", _options.ApiKey.Trim());
        request.Content = JsonContent.Create(new
        {
            from = $"+{remetente}", to = $"+{telefone}", content = texto.Trim(),
            request_id = "callmed:" + Guid.NewGuid().ToString("N")
        });
        try
        {
            using var response = await http.SendAsync(request, limite.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("httpSMS retornou HTTP {Status}.", (int)response.StatusCode);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return new CanalEnvioResultado
                    {
                        Erro = "httpSMS retornou HTTP 429. Confira a cota do plano e aguarde antes de tentar novamente.",
                        ProximaTentativaEm = DateTime.UtcNow.AddSeconds(60)
                    };
                if ((int)response.StatusCode >= 500) return SemConfirmacao();
                return CanalEnvioResultado.Falha(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "httpSMS recusou o acesso. Confira a API Key no Render.",
                    HttpStatusCode.NotFound => "httpSMS retornou HTTP 404. Confira BaseUrl e SendPath no Render.",
                    HttpStatusCode.UnprocessableEntity => "httpSMS rejeitou os dados. Confira o número From vinculado ao aplicativo e o destinatário com +55 e DDD.",
                    _ => $"httpSMS retornou HTTP {(int)response.StatusCode}. Consulte o painel do provedor."
                });
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(limite.Token));
            var root = json.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("status", out var envelopeStatus) &&
                (envelopeStatus.ValueKind != JsonValueKind.String || envelopeStatus.GetString() != "success"))
                return SemConfirmacao();
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                return SemConfirmacao();
            var status = data.TryGetProperty("status", out var state) && state.ValueKind == JsonValueKind.String ? state.GetString() : null;
            if (status is "failed" or "expired")
                return new CanalEnvioResultado { MensagemExternaId = id.GetString(), Erro = "httpSMS informou falha no envio. Confira as permissões, o chip e a conexão do aplicativo." };
            logger.LogInformation("SMS aceito pelo httpSMS. MessageId={MessageId}.", id.GetString());
            return new CanalEnvioResultado
            {
                Sucesso = true, MensagemExternaId = id.GetString(),
                StatusConfirmado = status switch
                {
                    "delivered" => StatusMensagemAtendimento.Entregue,
                    "sent" => StatusMensagemAtendimento.Enviada,
                    _ => StatusMensagemAtendimento.AguardandoEnvio
                }
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            logger.LogWarning("httpSMS não confirmou o resultado do envio. Tipo={Tipo}.", ex.GetType().Name);
            return SemConfirmacao();
        }
    }

    private static CanalEnvioResultado SemConfirmacao() => new()
    {
        Erro = "Envio sem confirmação do httpSMS. Confira o painel e o aplicativo antes de enviar novamente.",
        ReenvioBloqueado = true
    };
}
