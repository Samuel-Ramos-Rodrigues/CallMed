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
        app.UseStaticFiles(new StaticFileOptions
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
        });

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

        app.MapGet("/.well-known/assetlinks.json", (IConfiguration configuration) =>
        {
            if (!configuration.GetValue("Twa:Enabled", true))
                return Results.Json(Array.Empty<object>());

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

            if (fingerprints.Length == 0)
                return Results.Json(Array.Empty<object>());

            var statements = new[]
            {
                new
                {
                    relation = new[] { "delegate_permission/common.handle_all_urls" },
                    target = new
                    {
                        @namespace = "android_app",
                        package_name = packageName,
                        sha256_cert_fingerprints = fingerprints
                    }
                }
            };

            return Results.Json(statements);
        });

        app.MapRazorPages();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        return app;
    }
}
