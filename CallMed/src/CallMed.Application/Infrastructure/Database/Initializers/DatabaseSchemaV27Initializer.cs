using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

public sealed class DatabaseSchemaV27Initializer(MKSANContext context)
{
    public const string Sql = """
        ALTER TABLE "ConversasAtendimento" ADD COLUMN IF NOT EXISTS "IaTentativas" integer NOT NULL DEFAULT 0;
        ALTER TABLE "ConversasAtendimento" ADD COLUMN IF NOT EXISTS "IaPoliticaVersao" character varying(32) NULL;
        """;
    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
