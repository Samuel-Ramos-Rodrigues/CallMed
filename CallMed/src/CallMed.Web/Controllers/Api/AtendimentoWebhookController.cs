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
public sealed partial class AtendimentoWebhookController : ControllerBase
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
                    ct, somenteRegistrar: true);

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
}
