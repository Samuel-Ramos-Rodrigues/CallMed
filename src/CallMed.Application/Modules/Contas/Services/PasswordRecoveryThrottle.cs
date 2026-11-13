using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Usuarios;

// Counters live in the database: restarting or adding a Render instance does not
// reset the cooldown. Only SHA-256 bucket identifiers are stored, never CPF/email/IP.
public sealed class PasswordRecoveryThrottle(MKSANContext db)
{
    private static readonly SemaphoreSlim MemoryGate = new(1, 1);
    public async Task<bool> PermitirAsync(string identificador, string ip, CancellationToken ct = default)
    {
        var normalized = identificador.Trim().ToUpperInvariant();
        if (!normalized.Contains('@')) normalized = new string(normalized.Where(char.IsDigit).ToArray());
        foreach (var rule in new[] { ("ip:" + ip, 900, 10), ("account:" + normalized, 900, 3), ("cooldown:" + normalized, 60, 1) })
        {
            var now = DateTimeOffset.UtcNow; var window = now.ToUnixTimeSeconds() / rule.Item2;
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rule.Item1 + ":" + window)));
            var expires = DateTimeOffset.FromUnixTimeSeconds((window + 1) * rule.Item2).UtcDateTime;
            if (db.Database.IsRelational())
            {
                var affected = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SecurityBuckets\" (\"Id\",\"Contagem\",\"ExpiraEm\") VALUES ({id},1,{expires}) ON CONFLICT (\"Id\") DO UPDATE SET \"Contagem\"=\"SecurityBuckets\".\"Contagem\"+1 WHERE \"SecurityBuckets\".\"Contagem\" < {rule.Item3}", ct);
                if (affected != 1) return false;
            }
            else
            {
                await MemoryGate.WaitAsync(ct);
                try
                {
                    var bucket = await db.SecurityBuckets.FindAsync([id], ct);
                    if (bucket?.Contagem >= rule.Item3) return false;
                    if (bucket is null) db.SecurityBuckets.Add(new SecurityBucket { Id = id, Contagem = 1, ExpiraEm = expires });
                    else bucket.Contagem++;
                    await db.SaveChangesAsync(ct);
                }
                finally { MemoryGate.Release(); }
            }
        }
        return true;
    }
}
