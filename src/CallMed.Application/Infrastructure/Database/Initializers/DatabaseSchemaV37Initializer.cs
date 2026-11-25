using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Database;
public sealed class DatabaseSchemaV37Initializer(MKSANContext db)
{
    public const string Sql = """
ALTER TABLE "ContaSessoes" ADD COLUMN IF NOT EXISTS "RefreshVersion" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
CREATE TABLE IF NOT EXISTS "SecurityBuckets" ("Id" varchar(64) PRIMARY KEY, "Contagem" integer NOT NULL, "ExpiraEm" timestamp with time zone NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_SecurityBuckets_ExpiraEm" ON "SecurityBuckets" ("ExpiraEm");
CREATE TABLE IF NOT EXISTS "AtendimentoLeases" ("ConversaId" bigint PRIMARY KEY, "Token" uuid NOT NULL, "Ate" timestamp with time zone NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_SinaisTele_CriadoEm" ON "SinaisTele" ("CriadoEm");
""";
    public Task AplicarAsync(CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync(Sql, ct);
}
