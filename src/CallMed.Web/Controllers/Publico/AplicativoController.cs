using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AplicativoController(IWebHostEnvironment environment, ILogger<AplicativoController> logger) : Controller
{
    private string Pasta => Path.Combine(environment.ContentRootPath, "App_Data", "Aplicativo");
    private string Arquivo => Path.Combine(Pasta, "CallMed.apk");

    [HttpGet("/aplicativo")]
    public IActionResult Index() => View(Carregar());

    [HttpGet("/aplicativo/baixar")]
    public IActionResult Baixar()
    {
        var release = Carregar();
        if (!release.Disponivel) return NotFound("O aplicativo não está disponível para download neste momento.");
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return new PhysicalFileResult(Arquivo, "application/vnd.android.package-archive")
        {
            FileDownloadName = "CallMed.apk", EnableRangeProcessing = true,
            LastModified = new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(Arquivo)),
            EntityTag = new EntityTagHeaderValue("\"" + release.Sha256 + "\"")
        };
    }
    private AplicativoAndroidViewModel Carregar()
    {
        try
        {
            var json = System.IO.File.ReadAllText(Path.Combine(Pasta, "release.json"));
            var release = JsonSerializer.Deserialize<AplicativoAndroidViewModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            var info = new FileInfo(Arquivo);
            release.Disponivel = info.Exists && info.Length == release.TamanhoBytes && release.TamanhoBytes > 0 &&
                !string.IsNullOrWhiteSpace(release.Versao) && release.Sha256 is { Length: 64 } && release.Sha256.All(Uri.IsHexDigit);
            return release;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Pacote Android indisponível para download.");
            return new();
        }
    }
}
