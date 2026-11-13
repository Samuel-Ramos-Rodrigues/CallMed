using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

// Aditivo e idempotente, aplicado também aos bancos existentes no Render.
public sealed class DatabaseSchemaV28Initializer(MKSANContext context)
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS "AvaliacoesAtendimento" (
            "Id" uuid PRIMARY KEY,
            "ConversaAtendimentoId" bigint NOT NULL REFERENCES "ConversasAtendimento" ("Id") ON DELETE CASCADE,
            "UltimaEntradaId" bigint NOT NULL,
            "Canal" character varying(20) NOT NULL,
            "CriadoEm" timestamp with time zone NOT NULL,
            "Nota" integer NULL,
            "Comentario" character varying(1000) NULL,
            "RespondidoEm" timestamp with time zone NULL,
            "DispensadaEm" timestamp with time zone NULL,
            "RespostaExternaId" character varying(220) NULL,
            "ConviteProcessadoEm" timestamp with time zone NULL,
            "AgradecimentoProcessadoEm" timestamp with time zone NULL,
            "OrientacaoPendente" boolean NOT NULL DEFAULT false,
            "OrientacaoProcessadaEm" timestamp with time zone NULL,
            "Versao" uuid NOT NULL,
            CONSTRAINT "CK_Avaliacao_Nota" CHECK ("Nota" IS NULL OR "Nota" BETWEEN 1 AND 5)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_AvaliacoesAtendimento_ConversaAtendimentoId_UltimaEntradaId"
            ON "AvaliacoesAtendimento" ("ConversaAtendimentoId", "UltimaEntradaId");
        CREATE INDEX IF NOT EXISTS "IX_AvaliacoesAtendimento_CriadoEm" ON "AvaliacoesAtendimento" ("CriadoEm");
        """;
    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
