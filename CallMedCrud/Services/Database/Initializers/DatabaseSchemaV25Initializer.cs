using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

/// <summary>Registra a situação da matriz e o acompanhamento administrativo de elegibilidade nas consultas.</summary>
public sealed class DatabaseSchemaV25Initializer(MKSANContext context)
{
    public const string Sql = """
        ALTER TABLE "ConveniosEspecialidades" ADD COLUMN IF NOT EXISTS "StatusCobertura" character varying(24) NULL;
        ALTER TABLE "ConveniosEspecialidades" ADD COLUMN IF NOT EXISTS "RequerAutorizacao" boolean NOT NULL DEFAULT FALSE;
        UPDATE "ConveniosEspecialidades"
            SET "StatusCobertura" = CASE WHEN "Coberta" THEN 'Coberta' ELSE 'NaoCoberta' END
            WHERE "StatusCobertura" IS NULL OR btrim("StatusCobertura") = '';
        ALTER TABLE "ConveniosEspecialidades" ALTER COLUMN "StatusCobertura" SET DEFAULT 'VerificacaoManual';
        ALTER TABLE "ConveniosEspecialidades" ALTER COLUMN "StatusCobertura" SET NOT NULL;

        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "StatusElegibilidadeConvenio" character varying(32) NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "ObservacaoElegibilidadeConvenio" character varying(600) NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "ProtocoloAutorizacaoConvenio" character varying(120) NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "ElegibilidadeConvenioAtualizadaEm" timestamp with time zone NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "ElegibilidadeConvenioAtualizadaPorUsuarioId" character varying(450) NULL;
        UPDATE "Consultas"
            SET "StatusElegibilidadeConvenio" = CASE WHEN lower(coalesce("TipoPagamento", 'Particular')) IN ('convenio', 'convênio') THEN 'PendenteValidacao' ELSE 'NaoAplicavel' END
            WHERE "StatusElegibilidadeConvenio" IS NULL OR btrim("StatusElegibilidadeConvenio") = '';
        UPDATE "Consultas"
            SET "ObservacaoElegibilidadeConvenio" = 'Consulta existente: validar elegibilidade ativa, rede credenciada e autorização junto à operadora antes do atendimento.'
            WHERE "StatusElegibilidadeConvenio" = 'PendenteValidacao' AND "ObservacaoElegibilidadeConvenio" IS NULL;
        ALTER TABLE "Consultas" ALTER COLUMN "StatusElegibilidadeConvenio" SET DEFAULT 'NaoAplicavel';
        ALTER TABLE "Consultas" ALTER COLUMN "StatusElegibilidadeConvenio" SET NOT NULL;
        CREATE INDEX IF NOT EXISTS "IX_Consultas_StatusElegibilidadeConvenio_Data"
            ON "Consultas" ("StatusElegibilidadeConvenio", "Data");
    """;

    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
