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
        app.UseMiddleware<BrowserSecurityHeadersMiddleware>();

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
        // Android App Links são respondidos pela rota configurável, antes do arquivo estático.
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
        app.MapHealthChecks("/health/ready").AllowAnonymous();
        app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "CallMed" })).AllowAnonymous();
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "CallMed"
        }));

        app.MapGet("/.well-known/assetlinks.json", (IConfiguration configuration, IWebHostEnvironment environment) =>
        {
            // Twa:* is accepted only as an alias for already deployed Android link configuration.
            var enabled = configuration.GetValue("AndroidLinks:Enabled", configuration.GetValue("Twa:Enabled", true));
            if (!enabled) return Results.Json(Array.Empty<object>());
            var statements = new List<object>();
            var package = configuration["AndroidLinks:PackageName"] ?? configuration["Twa:PackageName"] ?? "com.callmed.app";
            var fingerprints = configuration.GetSection("AndroidLinks:Sha256CertFingerprints").Get<string[]>()
                ?? configuration.GetSection("Twa:Sha256CertFingerprints").Get<string[]>() ?? [];
            fingerprints = fingerprints.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
            if (fingerprints.Length > 0) statements.Add(new { relation = new[] { "delegate_permission/common.handle_all_urls" },
                target = new { @namespace = "android_app", package_name = package, sha256_cert_fingerprints = fingerprints } });
            var file = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), ".well-known", "assetlinks.json");
            if (File.Exists(file))
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    statements.AddRange(document.RootElement.EnumerateArray().Select(x => (object)x.Clone()));
            }
            return Results.Json(statements);
        });

        // Links anteriores continuam apontando ao fluxo próprio de recuperação.
        app.MapGet("/Identity/Account/ResetPassword", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Redirect("/Identity/Account/ForgotPassword" + context.Request.QueryString);
        }).AllowAnonymous();

        app.MapRazorPages();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
