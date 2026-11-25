using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Database;
public sealed class DatabaseSchemaV35Initializer(MKSANContext db) {
 public const string Sql="""
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "AreaProfissional" varchar(40) NOT NULL DEFAULT '';
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "RegistroConselho" varchar(20) NULL;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "RegistroNumero" varchar(30) NULL;
ALTER TABLE "Medicos" ADD COLUMN IF NOT EXISTS "RegistroUf" varchar(2) NULL;
UPDATE "Medicos" SET "AreaProfissional"='Psicologia' WHERE "AreaProfissional"='' AND "Crp" IS NOT NULL;
UPDATE "Medicos" SET "RegistroConselho"='CRP', "RegistroNumero"="Crp", "RegistroUf"='' WHERE "AreaProfissional"='Psicologia' AND "Crp" IS NOT NULL AND "RegistroNumero" IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Medicos_RegistroConselho_RegistroUf_RegistroNumero"
 ON "Medicos" ("RegistroConselho","RegistroUf","RegistroNumero") WHERE "RegistroNumero" IS NOT NULL;
""";
 public Task AplicarAsync(CancellationToken ct=default)=>db.Database.ExecuteSqlRawAsync(Sql,ct);
}
