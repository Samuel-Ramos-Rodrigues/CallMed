using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Database;
public sealed class DatabaseSchemaV33Initializer(MKSANContext db) {
 public const string Sql="""
CREATE TABLE IF NOT EXISTS "SaudeRotinas" (
 "Nome" varchar(80) PRIMARY KEY, "TentativaEm" timestamptz NOT NULL,
 "SucessoEm" timestamptz NULL, "FalhaEm" timestamptz NULL, "FalhasConsecutivas" integer NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS "PedidosPrivacidade" (
 "Id" uuid PRIMARY KEY, "UsuarioId" text NOT NULL, "Tipo" varchar(30) NOT NULL,
 "Estado" varchar(30) NOT NULL, "CriadoEm" timestamptz NOT NULL, "RespondidoEm" timestamptz NULL,
 "RespondidoPor" text NULL, "RespostaProtegida" text NULL, "Versao" uuid NOT NULL);
CREATE INDEX IF NOT EXISTS "IX_PedidosPrivacidade_UsuarioId" ON "PedidosPrivacidade" ("UsuarioId");
CREATE INDEX IF NOT EXISTS "IX_PedidosPrivacidade_Estado_CriadoEm" ON "PedidosPrivacidade" ("Estado","CriadoEm");
""";
 public Task AplicarAsync(CancellationToken ct=default)=>db.Database.ExecuteSqlRawAsync(Sql,ct);
}
