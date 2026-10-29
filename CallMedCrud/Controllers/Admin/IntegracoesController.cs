using Microsoft.AspNetCore.RateLimiting;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Atendimento.Canais.Email;
using MKSANCrud.Services.Email;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public sealed class IntegracoesController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly MKSANContext _context;

    public IntegracoesController(IConfiguration configuration, MKSANContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> EvolutionStatus(
        [FromServices] EvolutionWhatsAppSender evolution, CancellationToken ct)
    {
        var resultado = await evolution.VerificarConexaoAsync(ct);
        return Json(new { conectado = resultado.Conectado, mensagem = resultado.Mensagem });
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromServices] IEmailService emailService,
        [FromServices] SmsHttpSender smsSender,
        [FromServices] AtendimentoEmailReplyAddressService emailReplyAddresses,
        [FromServices] GmailApiClient gmailApi,
        CancellationToken ct)
    {
        ViewBag.WhatsAppAtivo = string.Equals(_configuration["Atendimento:WhatsApp:Evolution:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        ViewBag.GeminiConfigurado = !string.IsNullOrWhiteSpace(_configuration["Gemini:ApiKey"]);
        ViewBag.EmailConfigurado = emailService.Configurado;
        ViewBag.GmailInboxConfigurado = gmailApi.Configurado;
        ViewBag.EmailEntradaConfigurado = gmailApi.Configurado || emailReplyAddresses.Configurado;
        var gmailInboxEnabled = _configuration.GetValue<bool>("GmailInbox:Enabled");
        ViewBag.EmailEntradaProvider = gmailApi.Configurado
            ? "Gmail API"
            : gmailInboxEnabled
                ? "Gmail API pendente de configuração"
                : emailReplyAddresses.Configurado
                    ? "Brevo Inbound (legado)"
                    : "não configurado";
        ViewBag.SmsAtivo = smsSender.Configurado;
        ViewBag.LegacyConfigurada = !string.IsNullOrWhiteSpace(_configuration["LegacyIntegration:ApiKey"]);
        try { ViewBag.BancoConectado = await _context.Database.CanConnectAsync(ct); }
        catch { ViewBag.BancoConectado = false; }
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("email-test")]
    public async Task<IActionResult> TesteEmail(
        [FromForm] string destinatario,
        [FromServices] IEmailService emailService,
        CancellationToken ct)
    {
        var informado = destinatario?.Trim() ?? string.Empty;
        if (informado.Length > 254 ||
            !MailAddress.TryCreate(informado, out var email) ||
            !string.Equals(email.Address, informado, StringComparison.OrdinalIgnoreCase))
        {
            TempData["IntegracaoEmailMensagem"] = "Informe um endereço de e-mail válido.";
            TempData["IntegracaoEmailSucesso"] = false;
            return RedirectToAction(nameof(Index));
        }

        if (!emailService.Configurado)
        {
            TempData["IntegracaoEmailMensagem"] = "O SMTP ainda não está configurado. Confira as variáveis Smtp__* no Render.";
            TempData["IntegracaoEmailSucesso"] = false;
            return RedirectToAction(nameof(Index));
        }

        const string html = "<div style=\"font-family:Arial,sans-serif;line-height:1.6\"><h2>CallMed</h2><p>Este é um e-mail de teste do serviço SMTP do CallMed.</p><p>Se você recebeu esta mensagem, o envio SMTP da aplicação conseguiu entregar a mensagem ao servidor do provedor.</p></div>";
        var enviado = await emailService.EnviarAsync(
            email.Address,
            "Teste de e-mail - CallMed",
            html,
            ct);

        TempData["IntegracaoEmailSucesso"] = enviado;
        TempData["IntegracaoEmailMensagem"] = enviado
            ? "O servidor SMTP aceitou o e-mail de teste. Confira a caixa de entrada/spam e o painel de atividade do Brevo."
            : "O envio de teste falhou. Confira os logs do Render e a atividade de e-mail no Brevo. Verifique login/senha SMTP e remetente autorizado.";

        return RedirectToAction(nameof(Index));
    }

}
