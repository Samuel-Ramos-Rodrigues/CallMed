using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MKSANCrud.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MKSANCrud.Migrations;

[DbContext(typeof(MKSANContext))]
[Migration("20260824180000_V12LogicaSeguranca")]
public partial class V12LogicaSeguranca : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "UsuarioId",
            table: "Pacientes",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "Ativo",
            table: "Pacientes",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "UsuarioId",
            table: "Funcionarios",
            type: "text",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Especialidades",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Ativo = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Especialidades", x => x.Id);
            });

        migrationBuilder.AddColumn<int>(
            name: "EspecialidadeId",
            table: "Medicos",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ConversasAgente",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UsuarioId = table.Column<string>(type: "text", nullable: false),
                SessionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversasAgente", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversasAgente_AspNetUsers_UsuarioId",
                    column: x => x.UsuarioId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MensagensAgente",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ConversaAgenteId = table.Column<int>(type: "integer", nullable: false),
                Papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MensagensAgente", x => x.Id);
                table.ForeignKey(
                    name: "FK_MensagensAgente_ConversasAgente_ConversaAgenteId",
                    column: x => x.ConversaAgenteId,
                    principalTable: "ConversasAgente",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Normaliza estados antigos para o conjunto oficial da V12.
        migrationBuilder.Sql("""
            UPDATE "Consultas" SET "Status" = 'Pendente' WHERE lower("Status") = 'pendente';
            UPDATE "Consultas" SET "Status" = 'Confirmada' WHERE lower("Status") IN ('confirmada', 'confirmado');
            UPDATE "Consultas" SET "Status" = 'Remarcada' WHERE lower("Status") IN ('remarcada', 'remarcado');
            UPDATE "Consultas" SET "Status" = 'Cancelada' WHERE lower("Status") IN ('cancelada', 'cancelado');
            UPDATE "Consultas" SET "Status" = 'Realizada' WHERE lower("Status") IN ('realizada', 'realizado');
            """);

        migrationBuilder.Sql("""
            UPDATE "Pacientes" p
            SET "UsuarioId" = u."Id"
            FROM "AspNetUsers" u
            WHERE p."UsuarioId" IS NULL
              AND u."Email" IS NOT NULL
              AND lower(p."Email") = lower(u."Email");
            """);

        migrationBuilder.Sql("""
            UPDATE "Funcionarios" f
            SET "UsuarioId" = u."Id"
            FROM "AspNetUsers" u
            WHERE f."UsuarioId" IS NULL
              AND u."Email" IS NOT NULL
              AND lower(f."Email") = lower(u."Email");
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Especialidades_Nome",
            table: "Especialidades",
            column: "Nome",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Medicos_EspecialidadeId",
            table: "Medicos",
            column: "EspecialidadeId");

        migrationBuilder.CreateIndex(
            name: "IX_Pacientes_UsuarioId",
            table: "Pacientes",
            column: "UsuarioId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Funcionarios_UsuarioId",
            table: "Funcionarios",
            column: "UsuarioId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Consultas_PacienteId_Data",
            table: "Consultas",
            columns: new[] { "PacienteId", "Data" });

        migrationBuilder.CreateIndex(
            name: "IX_Consultas_Slot_Ativo",
            table: "Consultas",
            columns: new[] { "MedicoId", "Data", "Horario" },
            unique: true,
            filter: "lower(\"Status\") <> 'cancelada'");

        migrationBuilder.CreateIndex(
            name: "IX_ConversasAgente_UsuarioId_SessionId",
            table: "ConversasAgente",
            columns: new[] { "UsuarioId", "SessionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MensagensAgente_ConversaAgenteId_CriadoEm",
            table: "MensagensAgente",
            columns: new[] { "ConversaAgenteId", "CriadoEm" });

        migrationBuilder.AddForeignKey(
            name: "FK_Pacientes_AspNetUsers_UsuarioId",
            table: "Pacientes",
            column: "UsuarioId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Funcionarios_AspNetUsers_UsuarioId",
            table: "Funcionarios",
            column: "UsuarioId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Medicos_Especialidades_EspecialidadeId",
            table: "Medicos",
            column: "EspecialidadeId",
            principalTable: "Especialidades",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Pacientes_AspNetUsers_UsuarioId",
            table: "Pacientes");

        migrationBuilder.DropForeignKey(
            name: "FK_Funcionarios_AspNetUsers_UsuarioId",
            table: "Funcionarios");

        migrationBuilder.DropForeignKey(
            name: "FK_Medicos_Especialidades_EspecialidadeId",
            table: "Medicos");

        migrationBuilder.DropTable(name: "MensagensAgente");
        migrationBuilder.DropTable(name: "ConversasAgente");

        migrationBuilder.DropIndex(name: "IX_Pacientes_UsuarioId", table: "Pacientes");
        migrationBuilder.DropIndex(name: "IX_Funcionarios_UsuarioId", table: "Funcionarios");
        migrationBuilder.DropIndex(name: "IX_Consultas_PacienteId_Data", table: "Consultas");
        migrationBuilder.DropIndex(name: "IX_Consultas_Slot_Ativo", table: "Consultas");
        migrationBuilder.DropIndex(name: "IX_Medicos_EspecialidadeId", table: "Medicos");

        migrationBuilder.DropColumn(name: "UsuarioId", table: "Pacientes");
        migrationBuilder.DropColumn(name: "Ativo", table: "Pacientes");
        migrationBuilder.DropColumn(name: "UsuarioId", table: "Funcionarios");
        migrationBuilder.DropColumn(name: "EspecialidadeId", table: "Medicos");

        migrationBuilder.DropTable(name: "Especialidades");
    }
}
