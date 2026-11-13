using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Startup;
public sealed class DatabaseReadinessCheck(MKSANContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try { return await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Banco indisponível."); }
        catch (Exception) when (!ct.IsCancellationRequested) { return HealthCheckResult.Unhealthy("Banco indisponível."); }
    }
}
