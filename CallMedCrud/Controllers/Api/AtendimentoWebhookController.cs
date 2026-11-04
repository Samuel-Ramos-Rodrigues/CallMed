using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Options;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;
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
    private readonly GmailInboxOptions _gmailInbox;
    private readonly ILogger<AtendimentoWebhookController> _logger;

    public AtendimentoWebhookController(
        AtendimentoOrquestradorService orquestrador,
        AtendimentoEnvioService envio,
        IOptions<EvolutionWhatsAppOptions> whatsapp,
        IOptions<SmsHttpOptions> sms,
        IOptions<GmailInboxOptions> gmailInbox,
        ILogger<AtendimentoWebhookController> logger)
    {
        _orquestrador = orquestrador;
        _envio = envio;
        _whatsapp = whatsapp.Value;
        _sms = sms.Value;
        _gmailInbox = gmailInbox.Value;
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
            provider = "httpSMS",
            inboundConfigured =
                _sms.Enabled &&
                System.Text.Encoding.UTF8.GetByteCount(_sms.WebhookSecret ?? string.Empty) >= 32 &&
                AtendimentoIdentidadeService.NormalizarTelefone(_sms.From ?? string.Empty).Length is >= 12 and <= 15,
            outboundConfigured =
                _envio.CanalConfigurado(CanalAtendimento.Sms)
        },
        email = new
        {
            // Status de CONFIGURAÇÃO, não de conexão em tempo real com o provedor.
            enabled = _gmailInbox.Enabled || _envio.CanalConfigurado(CanalAtendimento.Email),
            inboundEnabled = _gmailInbox.Enabled,
            inboundConfigured = _gmailInbox.Configurado,
            gmailInboxConfigured = _gmailInbox.Configurado,
            outboundConfigured = _envio.CanalConfigurado(CanalAtendimento.Email),
            inboundDomainConfigured = !string.IsNullOrWhiteSpace(_gmailInbox.MailboxEmail),
            replyMode = _gmailInbox.Configurado ? "Gmail API; rascunho exige aprovação humana" : "Entrada não configurada"
        }
    });

    [HttpPost("whatsapp/evolution")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> WhatsAppEvolution(
        CancellationToken ct)
    {
        if (!_whatsapp.Enabled)
            return NotFound();

        if (!SegredoValido(_whatsapp.WebhookSecret))
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

                return StatusCode(
                    StatusCodes.Status500InternalServerError);
            }
        }

        return Ok();
    }

    [HttpPost("sms")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Sms(
        [FromServices] HttpSmsAtendimentoService recebimento, CancellationToken ct)
    {
        if (!_sms.Enabled) return NotFound();
        if (!HttpSmsWebhookValidator.Validar(Request.Headers.Authorization.ToString(), _sms.WebhookSecret))
            return Unauthorized();
        JsonDocument documento;
        try { documento = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct); }
        catch (JsonException) { return BadRequest(); }
        using (documento)
        {
            var root = documento.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return BadRequest();
            var evento = Texto(root, "type");
            var eventoHeader = Request.Headers["X-Event-Type"].ToString();
            if (string.IsNullOrWhiteSpace(evento) ||
                (!string.IsNullOrEmpty(eventoHeader) && eventoHeader != evento)) return BadRequest();
            if (evento is not ("message.phone.received" or "message.phone.sent" or "message.phone.delivered" or
                "message.send.failed" or "message.send.expired")) return Ok();
            if (Propriedade(root, "data") is not JsonElement dados || dados.ValueKind != JsonValueKind.Object)
                return BadRequest();
            var owner = AtendimentoIdentidadeService.NormalizarTelefone(Texto(dados, "owner") ?? string.Empty);
            var configurado = AtendimentoIdentidadeService.NormalizarTelefone(_sms.From ?? string.Empty);
            if (owner.Length < 12 || configurado.Length < 12) return BadRequest();
            if (!string.Equals(owner, configurado, StringComparison.Ordinal)) return Ok();
            var id = Texto(dados, "message_id") ?? Texto(dados, "id");
            if (string.IsNullOrWhiteSpace(id) || id.Length > 180) return BadRequest();
            try
            {
                if (evento != "message.phone.received")
                    return await recebimento.AtualizarStatusAsync(evento, id, dados, ct)
                        ? Ok() : StatusCode(StatusCodes.Status503ServiceUnavailable);
                var from = AtendimentoIdentidadeService.NormalizarTelefone(Texto(dados, "contact") ?? string.Empty);
                var texto = Texto(dados, "content");
                if (from.Length is < 12 or > 15 || string.IsNullOrWhiteSpace(texto) || texto.Length > 5000)
                    return BadRequest();
                await recebimento.RegistrarEntradaAsync(from, texto, id, ct);
                _logger.LogInformation("SMS recebido e persistido pelo httpSMS. MessageId={MessageId}.", id);
                return Ok(); // A IA roda no serviço de fundo, fora do limite de 5 s do webhook.
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao registrar evento httpSMS. Evento={Evento}, MessageId={MessageId}.", evento, id);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }

    private bool SegredoValido(string configurado)
    {
        var valido = AtendimentoWebhookSecretValidator.Validar(Request, configurado);
        if (!valido && string.IsNullOrWhiteSpace(configurado))
        {
            _logger.LogWarning(
                "Webhook de atendimento chamado sem segredo configurado.");
        }

        return valido;
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

        var data =
            Propriedade(root, "data") ?? root;

        var key =
            Propriedade(data, "key");

        string? jidPrincipal = null;
        string? keySenderPn = null;
        string? keyRemoteJidAlt = null;
        string? keyParticipant = null;

        if (key is JsonElement k)
        {
            id =
                Texto(k, "id") ?? string.Empty;

            jidPrincipal =
                Texto(k, "remoteJid");

            keySenderPn =
                Texto(k, "senderPn");

            keyRemoteJidAlt =
                Texto(k, "remoteJidAlt");

            keyParticipant =
                Texto(k, "participant");

            fromMe =
                Booleano(k, "fromMe") ?? false;
        }

        jidPrincipal ??=
            PrimeiroTexto(
                Texto(data, "remoteJid"),
                Texto(root, "remoteJid"));

        fromMe =
            Booleano(key, "fromMe") ??
            Booleano(data, "fromMe") ??
            Booleano(root, "fromMe") ??
            fromMe;

        remoteJid =
            EscolherJidEvolution(
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
            id =
                PrimeiroTexto(
                    Texto(data, "id"),
                    Texto(root, "id"),
                    Texto(root, "messageId")) ?? string.Empty;
        }

        var message =
            Propriedade(data, "message");

        texto =
            ExtrairTextoMensagem(message);

        if (string.IsNullOrWhiteSpace(texto))
        {
            texto =
                PrimeiroTexto(
                    Texto(data, "text"),
                    Texto(data, "body"),
                    Texto(root, "text"),
                    Texto(root, "body")) ?? string.Empty;
        }

        return
            !string.IsNullOrWhiteSpace(remoteJid) &&
            !string.IsNullOrWhiteSpace(texto);
    }

    private static string EscolherJidEvolution(
        string? principal,
        params string?[] alternativos)
    {
        principal =
            principal?.Trim();

        if (!string.IsNullOrWhiteSpace(principal) &&
            principal.Contains(
                "@g.us",
                StringComparison.OrdinalIgnoreCase))
        {
            return principal;
        }

        if (EhJidTelefone(principal))
            return principal!;

        var candidatos =
            alternativos
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToArray();

        var telefone =
            candidatos.FirstOrDefault(EhJidTelefone);

        if (!string.IsNullOrWhiteSpace(telefone))
            return telefone;

        var numerico =
            candidatos.FirstOrDefault(v =>
                !v.Contains('@') &&
                v.Count(char.IsDigit) >= 10);

        if (!string.IsNullOrWhiteSpace(numerico))
            return numerico;

        if (!string.IsNullOrWhiteSpace(principal) &&
            !principal.Contains(
                "@lid",
                StringComparison.OrdinalIgnoreCase))
        {
            return principal;
        }

        return
            candidatos.FirstOrDefault() ??
            principal ??
            string.Empty;
    }

    private static bool EhJidTelefone(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) &&
        (
            valor.Contains(
                "@s.whatsapp.net",
                StringComparison.OrdinalIgnoreCase) ||
            valor.Contains(
                "@c.us",
                StringComparison.OrdinalIgnoreCase)
        );

    private static string ExtrairTextoMensagem(
        JsonElement? mensagem,
        int nivel = 0)
    {
        if (mensagem is not JsonElement msg ||
            nivel > 6)
        {
            return string.Empty;
        }

        try
        {
            if (msg.ValueKind == JsonValueKind.String)
            {
                var bruto =
                    msg.GetString()?.Trim();

                if (!string.IsNullOrWhiteSpace(bruto) &&
                    (bruto.StartsWith('{') ||
                     bruto.StartsWith('[')))
                {
                    try
                    {
                        using var interno =
                            JsonDocument.Parse(bruto);

                        var extraido =
                            ExtrairTextoMensagem(
                                interno.RootElement,
                                nivel + 1);

                        if (!string.IsNullOrWhiteSpace(extraido))
                            return extraido;
                    }
                    catch (JsonException)
                    {
                    }
                }

                return bruto ?? string.Empty;
            }

            if (msg.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in
                         msg.EnumerateArray().Take(10))
                {
                    var extraido =
                        ExtrairTextoMensagem(
                            item,
                            nivel + 1);

                    if (!string.IsNullOrWhiteSpace(extraido))
                        return extraido;
                }

                return string.Empty;
            }

            if (msg.ValueKind != JsonValueKind.Object)
                return string.Empty;

            var direto =
                PrimeiroTexto(
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

            var nomesAninhados =
                new[]
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
                var nested =
                    Propriedade(msg, nome);

                var extraido =
                    ExtrairTextoMensagem(
                        nested,
                        nivel + 1);

                if (!string.IsNullOrWhiteSpace(extraido))
                    return extraido;
            }

            foreach (var prop in msg.EnumerateObject())
            {
                if (!EhCampoTextoEvolution(prop.Name))
                    continue;

                var extraido =
                    ExtrairTextoMensagem(
                        prop.Value,
                        nivel + 1);

                if (!string.IsNullOrWhiteSpace(extraido))
                    return extraido;
            }

            return string.Empty;
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException or
            ArgumentException)
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
            var dados =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            ExtrairStrings(
                root,
                dados,
                0);

            id =
                Primeiro(
                    dados,
                    "id",
                    "messageId",
                    "message_id") ?? string.Empty;

            var principal =
                Primeiro(
                    dados,
                    "remoteJid",
                    "remote_jid");

            remoteJid =
                EscolherJidEvolution(
                    principal,
                    Primeiro(
                        dados,
                        "senderPn",
                        "sender_pn"),
                    Primeiro(
                        dados,
                        "remoteJidAlt",
                        "remote_jid_alt"),
                    Primeiro(
                        dados,
                        "sender"),
                    Primeiro(
                        dados,
                        "participant"),
                    Primeiro(
                        dados,
                        "from",
                        "phone",
                        "number"));

            texto =
                Primeiro(
                    dados,
                    "conversation",
                    "text",
                    "caption",
                    "body",
                    "contentText",
                    "selectedDisplayText",
                    "selectedButtonId",
                    "selectedRowId") ?? string.Empty;

            fromMe =
                ExtrairBooleanoRecursivo(
                    root,
                    "fromMe",
                    0) ?? false;

            return
                !string.IsNullOrWhiteSpace(remoteJid) &&
                !string.IsNullOrWhiteSpace(texto);
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException or
            ArgumentException)
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
                foreach (var prop in
                         elemento.EnumerateObject())
                {
                    if (string.Equals(
                            prop.Name,
                            nome,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind ==
                            JsonValueKind.True)
                        {
                            return true;
                        }

                        if (prop.Value.ValueKind ==
                            JsonValueKind.False)
                        {
                            return false;
                        }

                        if (prop.Value.ValueKind ==
                                JsonValueKind.String &&
                            bool.TryParse(
                                prop.Value.GetString(),
                                out var parsed))
                        {
                            return parsed;
                        }
                    }

                    if (prop.Value.ValueKind is
                        JsonValueKind.Object or
                        JsonValueKind.Array)
                    {
                        var encontrado =
                            ExtrairBooleanoRecursivo(
                                prop.Value,
                                nome,
                                nivel + 1);

                        if (encontrado.HasValue)
                            return encontrado;
                    }
                }
            }
            else if (elemento.ValueKind ==
                     JsonValueKind.Array)
            {
                foreach (var item in
                         elemento.EnumerateArray().Take(10))
                {
                    var encontrado =
                        ExtrairBooleanoRecursivo(
                            item,
                            nome,
                            nivel + 1);

                    if (encontrado.HasValue)
                        return encontrado;
                }
            }
        }
        catch (Exception ex) when (
            ex is JsonException or
            InvalidOperationException or
            ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static bool EhCampoTextoEvolution(
        string nome) =>
        nome.Equals(
            "conversation",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "text",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "caption",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "body",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "contentText",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "selectedDisplayText",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "selectedButtonId",
            StringComparison.OrdinalIgnoreCase) ||
        nome.Equals(
            "selectedRowId",
            StringComparison.OrdinalIgnoreCase);

    private static string? PrimeiroTexto(
        params string?[] valores) =>
        valores
            .FirstOrDefault(
                v => !string.IsNullOrWhiteSpace(v))
            ?.Trim();

    private static string MascararIdentificador(
        string valor)
    {
        var digitos =
            new string(
                valor.Where(char.IsDigit).ToArray());

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
        {
            return null;
        }

        foreach (var p in e.EnumerateObject())
        {
            if (string.Equals(
                    p.Name,
                    nome,
                    StringComparison.OrdinalIgnoreCase))
            {
                return p.Value;
            }
        }

        return null;
    }

    private static string? Texto(
        JsonElement? elemento,
        string nome)
    {
        var valor =
            Propriedade(
                elemento,
                nome);

        return valor is JsonElement e &&
               e.ValueKind == JsonValueKind.String
            ? e.GetString()
            : null;
    }

    private static bool? Booleano(
        JsonElement? elemento,
        string nome)
    {
        var valor =
            Propriedade(
                elemento,
                nome);

        if (valor is not JsonElement e)
            return null;

        if (e.ValueKind == JsonValueKind.True)
            return true;

        if (e.ValueKind == JsonValueKind.False)
            return false;

        if (e.ValueKind == JsonValueKind.String &&
            bool.TryParse(
                e.GetString(),
                out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
