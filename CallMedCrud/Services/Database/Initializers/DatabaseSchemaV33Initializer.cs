using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Services.Database;

public sealed class DatabaseSchemaV33Initializer(MKSANContext context)
{
    public const string Sql = """
        ALTER TABLE "PreConsultasFormularios"
            ADD COLUMN IF NOT EXISTS "ConsentimentoEm" timestamp with time zone NULL;
        ALTER TABLE "PreConsultasFormularios" ALTER COLUMN "QueixaPrincipal" TYPE text;
        ALTER TABLE "PreConsultasFormularios" ALTER COLUMN "Alergias" TYPE text;
        ALTER TABLE "PreConsultasFormularios" ALTER COLUMN "MedicamentosEmUso" TYPE text;
        ALTER TABLE "PreConsultasFormularios" ALTER COLUMN "HistoricoRelevante" TYPE text;
        ALTER TABLE "PreConsultasFormularios" ALTER COLUMN "ConvenioNome" TYPE text;
        ALTER TABLE "PreConsultasFormularios" ALTER COLUMN "ConvenioCarteirinha" TYPE text;

        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "TelemedicinaSalaEsperaEm" timestamp with time zone NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "TelemedicinaAdmitidoEm" timestamp with time zone NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "TelemedicinaInicioEm" timestamp with time zone NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "TelemedicinaFimEm" timestamp with time zone NULL;
        ALTER TABLE "Consultas" ADD COLUMN IF NOT EXISTS "TelemedicinaRegistro" text NULL;
        """;

    public Task AplicarAsync(CancellationToken ct = default) => context.Database.ExecuteSqlRawAsync(Sql, ct);
}
