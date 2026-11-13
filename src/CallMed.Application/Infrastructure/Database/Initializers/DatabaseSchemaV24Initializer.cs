using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

/// <summary>Adiciona o fluxo de triagem administrativa para novos registros de exames.
/// Registros existentes são preservados como aprovados, pois já integravam o histórico.
/// </summary>
public sealed class DatabaseSchemaV24Initializer(MKSANContext context)
{
    public const string Sql = """
        ALTER TABLE "ExamesHistorico"
            ADD COLUMN IF NOT EXISTS "StatusTriagem" character varying(20) NOT NULL DEFAULT 'Aprovado';
        ALTER TABLE "ExamesHistorico"
            ADD COLUMN IF NOT EXISTS "TriadoEm" timestamp with time zone NULL;
        ALTER TABLE "ExamesHistorico"
            ADD COLUMN IF NOT EXISTS "TriadoPorUsuarioId" character varying(450) NULL;
        ALTER TABLE "ExamesHistorico"
            ADD COLUMN IF NOT EXISTS "ObservacaoTriagem" character varying(600) NULL;
        UPDATE "ExamesHistorico" SET "StatusTriagem" = 'Aprovado'
            WHERE "StatusTriagem" IS NULL OR btrim("StatusTriagem") = '';
        ALTER TABLE "ExamesHistorico" ALTER COLUMN "StatusTriagem" SET NOT NULL;
        ALTER TABLE "ExamesHistorico" ALTER COLUMN "StatusTriagem" SET DEFAULT 'Pendente';
        CREATE INDEX IF NOT EXISTS "IX_ExamesHistorico_StatusTriagem_Ativo_CriadoEm"
            ON "ExamesHistorico" ("StatusTriagem", "Ativo", "CriadoEm");
        """;

    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
