using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Clinica;
public sealed class EphemeralCleanupBackgroundService(IServiceScopeFactory scopes, ILogger<EphemeralCleanupBackgroundService> logger) : BackgroundService
{
    public static async Task<int> LimparAsync(MKSANContext db, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15); var expired = DateTime.UtcNow.AddHours(-1);
        var ids = await db.SinaisTele.Where(s => s.CriadoEm < cutoff).OrderBy(s => s.Id).Select(s => s.Id).Take(1000).ToListAsync(ct);
        if (db.Database.IsRelational())
        {
            if (ids.Count > 0) await db.SinaisTele.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(ct);
            var buckets = await db.SecurityBuckets.Where(b => b.ExpiraEm < expired).Select(b => b.Id).Take(1000).ToListAsync(ct);
            if (buckets.Count > 0) await db.SecurityBuckets.Where(b => buckets.Contains(b.Id)).ExecuteDeleteAsync(ct);
        }
        else { db.SinaisTele.RemoveRange(await db.SinaisTele.Where(s => ids.Contains(s.Id)).ToListAsync(ct)); db.SecurityBuckets.RemoveRange(await db.SecurityBuckets.Where(b => b.ExpiraEm < expired).Take(1000).ToListAsync(ct)); await db.SaveChangesAsync(ct); }
        return ids.Count;
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try { while (await timer.WaitForNextTickAsync(ct))
        { try { using var scope = scopes.CreateScope(); await LimparAsync(scope.ServiceProvider.GetRequiredService<MKSANContext>(), ct); }
          catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Falha na limpeza de dados efêmeros."); } }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
