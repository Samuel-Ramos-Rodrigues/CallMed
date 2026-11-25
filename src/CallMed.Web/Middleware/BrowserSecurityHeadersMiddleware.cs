namespace MKSANCrud.Middleware;
public sealed class BrowserSecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() => {
            var h = context.Response.Headers;
            h.TryAdd("X-Content-Type-Options", "nosniff");
            h.TryAdd("X-Frame-Options", "SAMEORIGIN");
            h.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            h.TryAdd("Permissions-Policy", "camera=(self), microphone=(self), geolocation=()");
            h.TryAdd("Content-Security-Policy-Report-Only", "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self'; form-action 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; media-src 'self' blob:; worker-src 'self'");
            // Frame/form/base protection is enforced; script nonces are staged in report-only
            // until all inline scripts can be migrated without breaking existing pages.
            h.TryAdd("Content-Security-Policy", "base-uri 'self'; object-src 'none'; frame-ancestors 'self'; form-action 'self'");
            return Task.CompletedTask;
        });
        return next(context);
    }
}
