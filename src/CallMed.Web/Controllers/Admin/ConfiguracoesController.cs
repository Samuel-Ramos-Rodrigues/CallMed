using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ConfiguracoesController : Controller
{
    private readonly IConfiguration _configuration;

    public ConfiguracoesController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.NomeClinica = _configuration["Clinica:Nome"] ?? "CallMed";
        ViewBag.TimeZone = _configuration["Clinica:TimeZone"] ?? "America/Maceio";
        ViewBag.PwaAtivo = true;
        return View();
    }
}
