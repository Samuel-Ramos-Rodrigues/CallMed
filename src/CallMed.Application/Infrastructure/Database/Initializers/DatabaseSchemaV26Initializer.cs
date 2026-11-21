using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

/// <summary>Progresso da IA por conversa, sem alterar mensagens existentes.</summary>
public sealed class DatabaseSchemaV26Initializer(MKSANContext context)
{
    public const string Sql = """
        ALTER TABLE "ConversasAtendimento" ADD COLUMN IF NOT EXISTS "IaEstado" character varying(32) NULL;
        ALTER TABLE "ConversasAtendimento" ADD COLUMN IF NOT EXISTS "IaEntradaId" bigint NULL;
        ALTER TABLE "ConversasAtendimento" ADD COLUMN IF NOT EXISTS "IaAtualizadoEm" timestamp with time zone NULL;
        """;

    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
