using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

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
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.WhatsAppAtivo = string.Equals(_configuration["Atendimento:WhatsApp:Evolution:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        ViewBag.GeminiConfigurado = !string.IsNullOrWhiteSpace(_configuration["Gemini:ApiKey"]);
        ViewBag.EmailConfigurado = !string.IsNullOrWhiteSpace(_configuration["Email:Smtp:Host"] ?? _configuration["Smtp:Host"]);
        ViewBag.SmsAtivo = string.Equals(_configuration["Atendimento:Sms:Http:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        ViewBag.LegacyConfigurada = !string.IsNullOrWhiteSpace(_configuration["LegacyIntegration:ApiKey"]);
        try { ViewBag.BancoConectado = await _context.Database.CanConnectAsync(ct); }
        catch { ViewBag.BancoConectado = false; }
        return View();
    }
}
