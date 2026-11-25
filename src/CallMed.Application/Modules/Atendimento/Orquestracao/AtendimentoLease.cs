using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Atendimento;

// A database lease serializes AI processing between different Render instances.
public sealed class AtendimentoLease(MKSANContext db, long id, Guid token) : IAsyncDisposable
{
    public static async Task<AtendimentoLease?> AdquirirAsync(MKSANContext db, long id, CancellationToken ct)
    {
        var token = Guid.NewGuid(); var now = DateTime.UtcNow; var until = now.AddMinutes(3);
        if (db.Database.IsRelational())
        {
            var n = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AtendimentoLeases\" (\"ConversaId\",\"Token\",\"Ate\") VALUES ({id},{token},{until}) ON CONFLICT (\"ConversaId\") DO UPDATE SET \"Token\"=EXCLUDED.\"Token\",\"Ate\"=EXCLUDED.\"Ate\" WHERE \"AtendimentoLeases\".\"Ate\" < {now}", ct);
            if (n != 1) return null;
        }
        return new(db, id, token);
    }
    public async ValueTask DisposeAsync()
    {
        if (db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"AtendimentoLeases\" WHERE \"ConversaId\"={id} AND \"Token\"={token}", CancellationToken.None);
    }
}
