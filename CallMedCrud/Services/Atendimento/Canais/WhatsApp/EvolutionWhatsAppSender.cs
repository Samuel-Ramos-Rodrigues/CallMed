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

    public bool Configurado => TentarObterEndpoints(out _, out _, out _);

    public async Task<(bool Conectado, string Mensagem)> VerificarConexaoAsync(CancellationToken ct = default)
    {
        var resultado = await DiagnosticarAsync(ct);
        return (resultado.Conectado, resultado.Mensagem);
    }

    public async Task<EvolutionDiagnosticoResultado> DiagnosticarAsync(CancellationToken ct = default)
    {
        if (!TentarObterEndpoints(out var consulta, out var envio, out var erro))
            return new(false, erro);

        EvolutionDiagnosticoResultado Resultado(bool conectado, string mensagem, int? status = null) =>
            new(conectado, mensagem, _options.InstanceName.Trim(), consulta, envio, status);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, consulta);
        request.Headers.TryAddWithoutValidation("apikey", _options.ApiKey);
        int? statusRecebido = null;
        try
        {
            using var response = await _http.SendAsync(request, limite.Token);
            statusRecebido = (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Resultado(false, "Evolution limitou as consultas (HTTP 429). Aguarde antes de verificar novamente.", 429);
            var corpo = await response.Content.ReadAsStringAsync(limite.Token);
            if (!response.IsSuccessStatusCode)
                return Resultado(false, DescreverFalha(response.StatusCode, corpo), (int)response.StatusCode);
            using var json = JsonDocument.Parse(corpo);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("instance", out var instance) || instance.ValueKind != JsonValueKind.Object ||
                !instance.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String)
                return Resultado(false, "Evolution respondeu, mas o estado da instância não foi reconhecido.", (int)response.StatusCode);
            return string.Equals(state.GetString(), "open", StringComparison.OrdinalIgnoreCase)
                ? Resultado(true, "A API confirmou a instância conectada. Esta consulta não testa o envio nem o recebimento de mensagens.", (int)response.StatusCode)
                : Resultado(false, "A instância está desconectada ou conectando. Confira o QR Code no painel da Evolution.", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Resultado(false, "Evolution não respondeu em até 8 segundos.");
        }
        catch (JsonException)
        {
            return Resultado(false, "O endereço respondeu, mas não retornou o JSON esperado da Evolution. Confira se BaseUrl aponta para a API, e não para o painel.", statusRecebido);
        }
        catch (HttpRequestException)
        {
            return Resultado(false, "Não foi possível consultar a Evolution. Confira o endereço e a rede do servidor.", statusRecebido);
        }
    }

    public async Task<CanalEnvioResultado> EnviarAsync(
        string destinatario,
        string texto,
        string? assunto = null,
        CancellationToken ct = default,
        string? replyTo = null)
    {
        if (!TentarObterEndpoints(out _, out var endpoint, out var erro))
            return CanalEnvioResultado.Falha(erro);

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
                        : CanalEnvioResultado.Falha(DescreverFalha(tentativa.StatusCode, tentativa.Corpo));
                }
                _controle.RegistrarSucesso();
                partesEnviadas++;
                ultimoId ??= TentarExtrairId(tentativa.Corpo);
            }
            return CanalEnvioResultado.Ok(ultimoId);
        }
        finally { _controle.Fila.Release(); }
    }

    private bool TentarObterEndpoints(out string consulta, out string envio, out string erro)
    {
        consulta = envio = string.Empty;
        erro = "Configure URL, chave e nome da instância da Evolution no servidor.";
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.InstanceName))
            return false;
        if (!Uri.TryCreate(_options.BaseUrl?.Trim(), UriKind.Absolute, out var url) ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            erro = "BaseUrl da Evolution precisa ser um endereço HTTP ou HTTPS válido.";
            return false;
        }
        if (!string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment))
        {
            erro = "BaseUrl da Evolution deve conter somente a raiz da API, sem credenciais, parâmetros ou fragmentos.";
            return false;
        }
        var caminho = url.AbsolutePath.TrimEnd('/');
        if (caminho.EndsWith("/manager", StringComparison.OrdinalIgnoreCase) ||
            caminho.EndsWith("/message/sendText", StringComparison.OrdinalIgnoreCase) ||
            caminho.Contains("/message/sendText/", StringComparison.OrdinalIgnoreCase) ||
            caminho.EndsWith("/instance/connectionState", StringComparison.OrdinalIgnoreCase) ||
            caminho.Contains("/instance/connectionState/", StringComparison.OrdinalIgnoreCase))
        {
            erro = "BaseUrl da Evolution deve apontar para a raiz da API, sem /manager e sem a rota de envio ou de consulta. Preserve o prefixo do proxy, se houver.";
            return false;
        }
        var instancia = _options.InstanceName.Trim();
        if (instancia is "." or "..")
        {
            erro = "Informe o nome exato da instância da Evolution em InstanceName.";
            return false;
        }
        var raiz = url.AbsoluteUri.TrimEnd('/');
        var nome = Uri.EscapeDataString(instancia);
        consulta = $"{raiz}/instance/connectionState/{nome}";
        envio = $"{raiz}/message/sendText/{nome}";
        return true;
    }

    private static string DescreverFalha(HttpStatusCode status, string corpo)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return $"Evolution recusou a autenticação ou o acesso (HTTP {(int)status}). Confira a chave e a permissão para a instância.";
        if (status != HttpStatusCode.NotFound)
            return $"Evolution retornou HTTP {(int)status}. Consulte Integrações → Verificar conexão com a Evolution.";

        // Identifica mensagens conhecidas e páginas HTML. Nunca repassa o corpo do provedor,
        // que pode conter telefone, texto enviado, chave ou outros dados privados.
        var mensagens = ExtrairMensagensDeErro(corpo);
        if (mensagens.Any(m =>
            (m.Contains("instance", StringComparison.OrdinalIgnoreCase) || m.Contains("instância", StringComparison.OrdinalIgnoreCase)) &&
            (m.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
             m.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
             m.Contains("não existe", StringComparison.OrdinalIgnoreCase) ||
             m.Contains("não encontrada", StringComparison.OrdinalIgnoreCase))))
            return "Evolution informou que a instância não existe ou não foi encontrada nesta API (HTTP 404). Compare o InstanceName exato e o servidor em Integrações.";
        if (mensagens.Any(m => m.StartsWith("Cannot POST ", StringComparison.OrdinalIgnoreCase) ||
                              m.StartsWith("Cannot GET ", StringComparison.OrdinalIgnoreCase)) ||
            corpo.TrimStart().StartsWith("Cannot POST ", StringComparison.OrdinalIgnoreCase) ||
            corpo.TrimStart().StartsWith("Cannot GET ", StringComparison.OrdinalIgnoreCase) ||
            corpo.Contains("<pre>Cannot POST ", StringComparison.OrdinalIgnoreCase) ||
            corpo.Contains("<pre>Cannot GET ", StringComparison.OrdinalIgnoreCase))
            return "O servidor não reconheceu a rota solicitada (HTTP 404). Confira a raiz da API, o prefixo do proxy e as rotas da versão instalada em Integrações.";
        if (corpo.TrimStart().StartsWith('<'))
            return "O endereço retornou uma página HTML com HTTP 404. Confira se BaseUrl aponta para a API e se o proxy publica a rota solicitada.";
        return "Evolution retornou HTTP 404 sem identificar se a falha é na rota ou na instância. Consulte o diagnóstico em Integrações.";
    }

    private static List<string> ExtrairMensagensDeErro(string corpo)
    {
        var mensagens = new List<string>();
        void Adicionar(JsonElement item)
        {
            if (item.ValueKind == JsonValueKind.String) mensagens.Add(item.GetString()!);
            else if (item.ValueKind == JsonValueKind.Array)
                foreach (var valor in item.EnumerateArray())
                    if (valor.ValueKind == JsonValueKind.String) mensagens.Add(valor.GetString()!);
        }
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return mensagens;
            if (root.TryGetProperty("message", out var message)) Adicionar(message);
            foreach (var campo in new[] { "response", "error" })
                if (root.TryGetProperty(campo, out var envelope) && envelope.ValueKind == JsonValueKind.Object &&
                    envelope.TryGetProperty("message", out var detalhe)) Adicionar(detalhe);
        }
        catch (JsonException) { }
        return mensagens;
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
