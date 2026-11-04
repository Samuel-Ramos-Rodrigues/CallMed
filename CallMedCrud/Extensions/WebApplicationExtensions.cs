using System.Globalization;
using Microsoft.AspNetCore.Localization;
using MKSANCrud.Middleware;

namespace MKSANCrud.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseCallMedPipeline(
        this WebApplication app,
        CultureInfo culture)
    {
        app.UseForwardedHeaders();

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(culture),
            SupportedCultures = [culture],
            SupportedUICultures = [culture]
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        // A resposta do assetlinks.json inclui tanto o app Android legado configurado
        // no Render quanto o app Bubblewrap descrito em wwwroot/.well-known/assetlinks.json.
        // Este unico caminho passa pela rota publica abaixo (nao pelo middleware estatico).
        app.UseWhen(
            context => !string.Equals(
                context.Request.Path.Value,
                "/.well-known/assetlinks.json",
                StringComparison.OrdinalIgnoreCase),
            branch => branch.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    var path = context.Context.Request.Path.Value ?? string.Empty;
    
                    if (string.Equals(
                            path,
                            "/service-worker.js",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        context.Context.Response.Headers["Cache-Control"] =
                            "no-cache, no-store, must-revalidate";
                        context.Context.Response.Headers["Pragma"] = "no-cache";
                        context.Context.Response.Headers["Expires"] = "0";
                    }
                    else if (
                        string.Equals(path, "/manifest.json", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Context.Response.Headers["Cache-Control"] =
                            "no-cache, must-revalidate";
                    }
                }
            }));

        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<ActiveAccountMiddleware>();
        app.UseAuthorization();
        app.UseRateLimiter();

        return app;
    }

    public static WebApplication MapCallMedEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "CallMed"
        }));

        app.MapGet("/.well-known/assetlinks.json", (IConfiguration configuration, IWebHostEnvironment environment) =>
        {
            if (!configuration.GetValue("Twa:Enabled", true))
                return Results.Json(Array.Empty<object>());

            var statements = new List<object>();

            // Compatibilidade: preserva a TWA existente (com.callmed.app),
            // incluindo SHA-256 adicionais configurados no Render (ex.: Play App Signing).
            var packageName = configuration["Twa:PackageName"]?.Trim();
            if (string.IsNullOrWhiteSpace(packageName))
                packageName = "com.callmed.app";

            var fingerprints = configuration
                .GetSection("Twa:Sha256CertFingerprints")
                .Get<string[]>()?
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Select(valor => valor.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];

            if (fingerprints.Length > 0)
            {
                statements.Add(new
                {
                    relation = new[] { "delegate_permission/common.handle_all_urls" },
                    target = new
                    {
                        @namespace = "android_app",
                        package_name = packageName,
                        sha256_cert_fingerprints = fingerprints
                    }
                });
            }

            // O TWA gerado no Bubblewrap tem outro package name e certificado.
            // A declaracao e mantida em um arquivo versionado, sem dados privados.
            var webRootPath = environment.WebRootPath
                ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            var filePath = Path.Combine(webRootPath, ".well-known", "assetlinks.json");
            if (File.Exists(filePath))
            {
                using var jsonDocument = System.Text.Json.JsonDocument.Parse(File.ReadAllText(filePath));
                if (jsonDocument.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var statement in jsonDocument.RootElement.EnumerateArray())
                        statements.Add(statement.Clone());
                }
            }

            return Results.Json(statements);
        });

        app.MapRazorPages();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
