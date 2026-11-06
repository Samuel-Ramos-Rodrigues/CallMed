using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

public sealed class DatabaseSchemaV31Initializer(MKSANContext context)
{
    public const string Sql = """
        ALTER TABLE "Consultas"
            ADD COLUMN IF NOT EXISTS "CheckInEm" timestamp with time zone NULL;
        """;

    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
