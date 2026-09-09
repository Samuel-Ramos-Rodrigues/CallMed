using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.Email;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;

namespace MKSANCrud.Controllers;

[ApiController]
[Route("api/atendimento")]
[EnableRateLimiting("webhooks")]
public sealed class AtendimentoWebhookController : ControllerBase
{
    private readonly AtendimentoOrquestradorService _orquestrador;
    private readonly AtendimentoEnvioService _envio;
    private readonly EvolutionWhatsAppOptions _whatsapp;
    private readonly SmsHttpOptions _sms;
    private readonly EmailInboundOptions _email;
    private readonly ILogger<AtendimentoWebhookController> _logger;

    public AtendimentoWebhookController(
        AtendimentoOrquestradorService orquestrador,
        AtendimentoEnvioService envio,
        IOptions<EvolutionWhatsAppOptions> whatsapp,
        IOptions<SmsHttpOptions> sms,
        IOptions<EmailInboundOptions> email,
        ILogger<AtendimentoWebhookController> logger)
    {
        _orquestrador = orquestrador;
        _envio = envio;
        _whatsapp = whatsapp.Value;
        _sms = sms.Value;
        _email = email.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    [Authorize(Roles = "Funcionario,Admin")]
    public IActionResult Status() => Ok(new
    {
        web = new
        {
            enabled = true,
            outboundConfigured = true
        },
        whatsapp = new
        {
            enabled = _whatsapp.Enabled,
            instanceName = _whatsapp.InstanceName,
            baseUrlConfigured =
                Uri.TryCreate(_whatsapp.BaseUrl, UriKind.Absolute, out _),
            publicNumberConfigured =
                !string.IsNullOrWhiteSpace(_whatsapp.PublicNumber),
            inboundConfigured =
                _whatsapp.Enabled &&
                !string.IsNullOrWhiteSpace(_whatsapp.WebhookSecret),
            outboundConfigured =
                _envio.CanalConfigurado(CanalAtendimento.WhatsApp)
        },
        sms = new
        {
            enabled = _sms.Enabled,
            inboundConfigured =
                _sms.Enabled &&
                !string.IsNullOrWhiteSpace(_sms.WebhookSecret),
            outboundConfigured =
                _envio.CanalConfigurado(CanalAtendimento.Sms)
        },
        email = new
        {
            inboundEnabled = _email.Enabled,
            inboundConfigured =
                _email.Enabled &&
                !string.IsNullOrWhiteSpace(_email.WebhookSecret),
            outboundConfigured =
                _envio.CanalConfigurado(CanalAtendimento.Email)
        }
    });

    [HttpPost("whatsapp/evolution")]
    public async Task<IActionResult> WhatsAppEvolution(
        [FromQuery] string? secret,
        CancellationToken ct)
    {
        if (!_whatsapp.Enabled)
            return NotFound();

        if (!SegredoValido(_whatsapp.WebhookSecret, secret))
            return Unauthorized();

        JsonDocument doc;

        try
        {
            doc = await JsonDocument.ParseAsync(
                Request.Body,
                cancellationToken: ct);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        using (doc)
        {
            var root = doc.RootElement;
            var evento = Texto(root, "event") ?? "desconhecido";
            var instancia = Texto(root, "instance") ?? _whatsapp.InstanceName;

            bool extraido;
            string id;
            string telefone;
            string texto;
            bool fromMe;

            try
            {
                extraido = TentarExtrairEvolution(
                    root,
                    out id,
                    out telefone,
                    out texto,
                    out fromMe);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Webhooks externos não podem derrubar o endpoint só porque uma
                // versão da Evolution alterou o formato de algum campo. Quando o
                // parser estruturado não reconhecer o payload, tenta um parser
                // genérico e mantém o endpoint respondendo de forma controlada.
                _logger.LogWarning(
                    ex,
                    "Parser estruturado da Evolution falhou. Evento={Evento}, instância={Instancia}. Tentando fallback compatível.",
                    evento,
                    instancia);

                extraido = TentarExtrairEvolutionFallback(
                    root,
                    out id,
                    out telefone,
                    out texto,
                    out fromMe);
            }

            if (!extraido)
            {
                _logger.LogDebug(
                    "Webhook Evolution ignorado. Evento={Evento}, instância={Instancia}: payload sem mensagem de texto utilizável.",
                    evento,
                    instancia);
                return Ok();
            }

            // O canal da clínica é individual. Mensagens de grupos não entram
            // no atendimento nem são usadas para identificar pacientes.
            if (telefone.Contains("@g.us", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Mensagem de grupo ignorada no webhook Evolution. Mensagem={MensagemId}.",
                    id);
                return Ok();
            }

            if (fromMe || string.IsNullOrWhiteSpace(texto))
                return Ok();

            _logger.LogInformation(
                "Evolution inbound recebido. Evento={Evento}, instância={Instancia}, mensagem={MensagemId}, remetente={Remetente}.",
                evento,
                instancia,
                id,
                MascararIdentificador(telefone));

            try
            {
                await _orquestrador.ProcessarEntradaAsync(
                    new CanalMensagemEntrada
                    {
                        Canal = CanalAtendimento.WhatsApp,
                        Identificador = telefone,
                        Texto = texto,
                        MensagemExternaId = id
                    },
                    ct);

                _logger.LogInformation(
                    "Evolution inbound processado. Mensagem={MensagemId}, remetente={Remetente}.",
                    id,
                    MascararIdentificador(telefone));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Falha ao processar webhook Evolution. Evento={Evento}, instância={Instancia}, mensagem={MensagemId}, remetente={Remetente}.",
                    evento,
                    instancia,
                    id,
                    MascararIdentificador(telefone));

                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        return Ok();
    }

    [HttpPost("sms")]
    public async Task<IActionResult> Sms(
        [FromQuery] string? secret,
        CancellationToken ct)
    {
        if (!_sms.Enabled)
            return NotFound();

        if (!SegredoValido(_sms.WebhookSecret, secret))
            return Unauthorized();

        var dados = await LerEntradaGenericaAsync(ct);

        var from = Primeiro(
            dados,
            "from",
            "sender",
            "phone",
            "msisdn",
            "mobile");

        var texto = Primeiro(
            dados,
            "text",
            "message",
            "body",
            "content");

        var id = Primeiro(
            dados,
            "id",
            "messageId",
            "message_id",
            "smsId");

        if (string.IsNullOrWhiteSpace(from) ||
            string.IsNullOrWhiteSpace(texto))
            return BadRequest();

        await _orquestrador.ProcessarEntradaAsync(
            new CanalMensagemEntrada
            {
                Canal = CanalAtendimento.Sms,
                Identificador = from,
                Texto = texto,
                MensagemExternaId = id
            },
            ct);

        return Ok();
    }

    [HttpPost("email")]
    public async Task<IActionResult> Email(
        [FromQuery] string? secret,
        CancellationToken ct)
    {
        if (!_email.Enabled)
            return NotFound();

        if (!SegredoValido(_email.WebhookSecret, secret))
            return Unauthorized();

        var dados = await LerEntradaGenericaAsync(ct);

        var from = Primeiro(
            dados,
            "from",
            "sender",
            "email",
            "From");

        var assunto = Primeiro(
            dados,
            "subject",
            "Subject");

        var texto = Primeiro(
            dados,
            "text",
            "body",
            "stripped-text",
            "TextBody",
            "plain");

        var id = Primeiro(
            dados,
            "id",
            "messageId",
            "message_id",
            "MessageID",
            "Message-Id");

        if (string.IsNullOrWhiteSpace(from) ||
            string.IsNullOrWhiteSpace(texto))
            return BadRequest();

        var email = ExtrairEmail(from);

        await _orquestrador.ProcessarEntradaAsync(
            new CanalMensagemEntrada
            {
                Canal = CanalAtendimento.Email,
                Identificador = email,
                Texto = texto,
                MensagemExternaId = id,
                Assunto = assunto
            },
            ct);

        return Ok();
    }

    private bool SegredoValido(
        string configurado,
        string? querySecret)
    {
        if (string.IsNullOrWhiteSpace(configurado))
        {
            _logger.LogWarning(
                "Webhook de atendimento chamado sem segredo configurado.");
            return false;
        }

        var header =
            Request.Headers["X-MKSAN-Webhook-Secret"]
                .ToString();

        var recebido =
            !string.IsNullOrWhiteSpace(header)
                ? header
                : querySecret;

        if (string.IsNullOrWhiteSpace(recebido))
            return false;

        var esperado = Encoding.UTF8.GetBytes(configurado);
        var informado = Encoding.UTF8.GetBytes(recebido);

        return esperado.Length == informado.Length &&
               CryptographicOperations.FixedTimeEquals(
                   esperado,
                   informado);
    }

    private async Task<Dictionary<string, string>>
        LerEntradaGenericaAsync(CancellationToken ct)
    {
        var resultado =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);

            foreach (var item in form)
                resultado[item.Key] = item.Value.ToString();

            return resultado;
        }

        try
        {
            using var doc = await JsonDocument.ParseAsync(
                Request.Body,
                cancellationToken: ct);

            ExtrairStrings(
                doc.RootElement,
                resultado,
                0);
        }
        catch (JsonException)
        {
            // Retorna vazio e o endpoint responde BadRequest.
        }

        return resultado;
    }

    private static void ExtrairStrings(
        JsonElement element,
        IDictionary<string, string> destino,
        int nivel)
    {
        if (nivel > 4)
            return;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    if (!destino.ContainsKey(prop.Name))
                        destino[prop.Name] =
                            prop.Value.GetString() ?? string.Empty;
                }
                else if (prop.Value.ValueKind is
                         JsonValueKind.Object or
                         JsonValueKind.Array)
                {
                    ExtrairStrings(
                        prop.Value,
                        destino,
                        nivel + 1);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray().Take(10))
                ExtrairStrings(item, destino, nivel + 1);
        }
    }

    private static string? Primeiro(
        IReadOnlyDictionary<string, string> dados,
        params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            if (dados.TryGetValue(nome, out var valor) &&
                !string.IsNullOrWhiteSpace(valor))
                return valor.Trim();
        }

        return null;
    }

    private static string ExtrairEmail(string valor)
    {
        var inicio = valor.LastIndexOf('<');
        var fim = valor.LastIndexOf('>');

        if (inicio >= 0 && fim > inicio)
            return valor[(inicio + 1)..fim]
                .Trim()
                .ToLowerInvariant();

        return valor.Trim().ToLowerInvariant();
    }

    private static bool TentarExtrairEvolution(
        JsonElement root,
        out string id,
        out string remoteJid,
        out string texto,
        out bool fromMe)
    {
        id = string.Empty;
        remoteJid = string.Empty;
        texto = string.Empty;
        fromMe = false;

        var data = Propriedade(root, "data") ?? root;
        var key = Propriedade(data, "key");

        string? jidPrincipal = null;
        string? keySenderPn = null;
        string? keyRemoteJidAlt = null;
        string? keyParticipant = null;

        if (key is JsonElement k)
        {
            id = Texto(k, "id") ?? string.Empty;
            jidPrincipal = Texto(k, "remoteJid");
            keySenderPn = Texto(k, "senderPn");
            keyRemoteJidAlt = Texto(k, "remoteJidAlt");
            keyParticipant = Texto(k, "participant");
            fromMe = Booleano(k, "fromMe") ?? false;
        }

        jidPrincipal ??= PrimeiroTexto(
            Texto(data, "remoteJid"),
            Texto(root, "remoteJid"));

        fromMe = Booleano(key, "fromMe") ??
                 Booleano(data, "fromMe") ??
                 Booleano(root, "fromMe") ??
                 fromMe;

        remoteJid = EscolherJidEvolution(
            jidPrincipal,
            keySenderPn,
            keyRemoteJidAlt,
            Texto(data, "senderPn"),
            Texto(data, "remoteJidAlt"),
            Texto(data, "sender"),
            Texto(root, "senderPn"),
            Texto(root, "sender"),
            keyParticipant);

        if (string.IsNullOrWhiteSpace(id))
        {
            id = PrimeiroTexto(
                Texto(data, "id"),
                Texto(root, "id"),
                Texto(root, "messageId")) ?? string.Empty;
        }

        var message = Propriedade(data, "message");
        texto = ExtrairTextoMensagem(message);

        if (string.IsNullOrWhiteSpace(texto))
        {
            texto = PrimeiroTexto(
                Texto(data, "text"),
                Texto(data, "body"),
                Texto(root, "text"),
                Texto(root, "body")) ?? string.Empty;
        }

        return !string.IsNullOrWhiteSpace(remoteJid) &&
               !string.IsNullOrWhiteSpace(texto);
    }

    private static string EscolherJidEvolution(
        string? principal,
        params string?[] alternativos)
    {
        principal = principal?.Trim();

        // Grupos precisam continuar identificados como grupo para serem
        // descartados pelo endpoint antes de qualquer vínculo com paciente.
        if (!string.IsNullOrWhiteSpace(principal) &&
            principal.Contains("@g.us", StringComparison.OrdinalIgnoreCase))
            return principal;

        // JID telefônico clássico continua sendo a fonte preferida.
        if (EhJidTelefone(principal))
            return principal!;

        var candidatos = alternativos
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .ToArray();

        // Evolution/WhatsApp recentes podem entregar remoteJid como @lid e o
        // número real em senderPn/remoteJidAlt. Prefere explicitamente um JID
        // telefônico para não armazenar o LID como identificador do paciente.
        var telefone = candidatos.FirstOrDefault(EhJidTelefone);
        if (!string.IsNullOrWhiteSpace(telefone))
            return telefone;

        var numerico = candidatos.FirstOrDefault(v =>
            !v.Contains('@') && v.Count(char.IsDigit) >= 10);
        if (!string.IsNullOrWhiteSpace(numerico))
            return numerico;

        if (!string.IsNullOrWhiteSpace(principal) &&
            !principal.Contains("@lid", StringComparison.OrdinalIgnoreCase))
            return principal;

        return candidatos.FirstOrDefault() ?? principal ?? string.Empty;
    }

    private static bool EhJidTelefone(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) &&
        (valor.Contains("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase) ||
         valor.Contains("@c.us", StringComparison.OrdinalIgnoreCase));

    private static string ExtrairTextoMensagem(
        JsonElement? mensagem,
        int nivel = 0)
    {
        if (mensagem is not JsonElement msg || nivel > 6)
            return string.Empty;

        try
        {
            if (msg.ValueKind == JsonValueKind.String)
            {
                var bruto = msg.GetString()?.Trim();

                // Algumas integrações encapsulam `message` como JSON serializado.
                // Tenta interpretar sem transformar uma mudança externa em HTTP 500.
                if (!string.IsNullOrWhiteSpace(bruto) &&
                    (bruto.StartsWith('{') || bruto.StartsWith('[')))
                {
                    try
                    {
                        using var interno = JsonDocument.Parse(bruto);
                        var extraido = ExtrairTextoMensagem(interno.RootElement, nivel + 1);
                        if (!string.IsNullOrWhiteSpace(extraido))
                            return extraido;
                    }
                    catch (JsonException)
                    {
                        // Se não for JSON de verdade, o próprio texto continua válido.
                    }
                }

                return bruto ?? string.Empty;
            }

            if (msg.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in msg.EnumerateArray().Take(10))
                {
                    var extraido = ExtrairTextoMensagem(item, nivel + 1);
                    if (!string.IsNullOrWhiteSpace(extraido))
                        return extraido;
                }

                return string.Empty;
            }

            if (msg.ValueKind != JsonValueKind.Object)
                return string.Empty;

            var direto = PrimeiroTexto(
                Texto(msg, "conversation"),
                Texto(msg, "text"),
                Texto(msg, "caption"),
                Texto(msg, "body"),
                Texto(msg, "contentText"),
                Texto(msg, "selectedDisplayText"),
                Texto(msg, "selectedButtonId"),
                Texto(msg, "selectedRowId"));

            if (!string.IsNullOrWhiteSpace(direto))
                return direto;

            var nomesAninhados = new[]
            {
                "extendedTextMessage",
                "imageMessage",
                "videoMessage",
                "documentMessage",
                "buttonsResponseMessage",
                "templateButtonReplyMessage",
                "listResponseMessage",
                "singleSelectReply",
                "ephemeralMessage",
                "viewOnceMessage",
                "viewOnceMessageV2",
                "viewOnceMessageV2Extension",
                "editedMessage",
                "protocolMessage",
                "message"
            };

            foreach (var nome in nomesAninhados)
            {
                var nested = Propriedade(msg, nome);
                var extraido = ExtrairTextoMensagem(nested, nivel + 1);

                if (!string.IsNullOrWhiteSpace(extraido))
                    return extraido;
            }

            // Último fallback dentro do próprio objeto: procura apenas campos com
            // nomes textuais conhecidos. Não lê messageSecret/chaves/base64.
            foreach (var prop in msg.EnumerateObject())
            {
                if (!EhCampoTextoEvolution(prop.Name))
                    continue;

                var extraido = ExtrairTextoMensagem(prop.Value, nivel + 1);
                if (!string.IsNullOrWhiteSpace(extraido))
                    return extraido;
            }

            return string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            return string.Empty;
        }
    }

    private static bool TentarExtrairEvolutionFallback(
        JsonElement root,
        out string id,
        out string remoteJid,
        out string texto,
        out bool fromMe)
    {
        id = string.Empty;
        remoteJid = string.Empty;
        texto = string.Empty;
        fromMe = false;

        try
        {
            var dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ExtrairStrings(root, dados, 0);

            id = Primeiro(dados, "id", "messageId", "message_id") ?? string.Empty;

            var principal = Primeiro(dados, "remoteJid", "remote_jid");
            remoteJid = EscolherJidEvolution(
                principal,
                Primeiro(dados, "senderPn", "sender_pn"),
                Primeiro(dados, "remoteJidAlt", "remote_jid_alt"),
                Primeiro(dados, "sender"),
                Primeiro(dados, "participant"),
                Primeiro(dados, "from", "phone", "number"));

            texto = Primeiro(
                dados,
                "conversation",
                "text",
                "caption",
                "body",
                "contentText",
                "selectedDisplayText",
                "selectedButtonId",
                "selectedRowId") ?? string.Empty;

            fromMe = ExtrairBooleanoRecursivo(root, "fromMe", 0) ?? false;

            return !string.IsNullOrWhiteSpace(remoteJid) &&
                   !string.IsNullOrWhiteSpace(texto);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            id = string.Empty;
            remoteJid = string.Empty;
            texto = string.Empty;
            fromMe = false;
            return false;
        }
    }

    private static bool? ExtrairBooleanoRecursivo(
        JsonElement elemento,
        string nome,
        int nivel)
    {
        if (nivel > 6)
            return null;

        try
        {
            if (elemento.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in elemento.EnumerateObject())
                {
                    if (string.Equals(prop.Name, nome, StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind == JsonValueKind.True)
                            return true;
                        if (prop.Value.ValueKind == JsonValueKind.False)
                            return false;
                        if (prop.Value.ValueKind == JsonValueKind.String &&
                            bool.TryParse(prop.Value.GetString(), out var parsed))
                            return parsed;
                    }

                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        var encontrado = ExtrairBooleanoRecursivo(prop.Value, nome, nivel + 1);
                        if (encontrado.HasValue)
                            return encontrado;
                    }
                }
            }
            else if (elemento.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elemento.EnumerateArray().Take(10))
                {
                    var encontrado = ExtrairBooleanoRecursivo(item, nome, nivel + 1);
                    if (encontrado.HasValue)
                        return encontrado;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static bool EhCampoTextoEvolution(string nome) =>
        nome.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("text", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("caption", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("body", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("contentText", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("selectedDisplayText", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("selectedButtonId", StringComparison.OrdinalIgnoreCase) ||
        nome.Equals("selectedRowId", StringComparison.OrdinalIgnoreCase);

    private static string? PrimeiroTexto(params string?[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string MascararIdentificador(string valor)
    {
        var digitos = new string(valor.Where(char.IsDigit).ToArray());

        if (digitos.Length <= 4)
            return "****";

        return $"***{digitos[^4..]}";
    }

    private static JsonElement? Propriedade(
        JsonElement? elemento,
        string nome)
    {
        if (elemento is not JsonElement e ||
            e.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var p in e.EnumerateObject())
        {
            if (string.Equals(
                    p.Name,
                    nome,
                    StringComparison.OrdinalIgnoreCase))
                return p.Value;
        }

        return null;
    }

    private static string? Texto(
        JsonElement? elemento,
        string nome)
    {
        var valor = Propriedade(elemento, nome);

        return valor is JsonElement e &&
               e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
    }

    private static bool? Booleano(
        JsonElement? elemento,
        string nome)
    {
        var valor = Propriedade(elemento, nome);

        if (valor is not JsonElement e)
            return null;

        if (e.ValueKind == JsonValueKind.True)
            return true;

        if (e.ValueKind == JsonValueKind.False)
            return false;

        if (e.ValueKind == JsonValueKind.String &&
            bool.TryParse(e.GetString(), out var parsed))
            return parsed;

        return null;
    }
}
