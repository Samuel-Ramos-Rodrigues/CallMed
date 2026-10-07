using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MKSANCrud.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MKSANCrud.Migrations;

[DbContext(typeof(MKSANContext))]
[Migration("20260829203000_V14CallMedAgendaSemanal")]
public partial class V14CallMedAgendaSemanal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "OrigemAgendaSemanal",
            table: "Disponibilidades",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "MedicoHorariosSemanais",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MedicoId = table.Column<int>(type: "integer", nullable: false),
                DiaSemana = table.Column<int>(type: "integer", nullable: false),
                Horario = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                Ativo = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MedicoHorariosSemanais", x => x.Id);
                table.ForeignKey(
                    name: "FK_MedicoHorariosSemanais_Medicos_MedicoId",
                    column: x => x.MedicoId,
                    principalTable: "Medicos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MedicoHorariosSemanais_MedicoId_DiaSemana_Horario",
            table: "MedicoHorariosSemanais",
            columns: new[] { "MedicoId", "DiaSemana", "Horario" },
            unique: true);

        // Remove Cardiologia do catálogo padrão apenas quando ela não estiver em uso.
        migrationBuilder.Sql("""
            DELETE FROM "Especialidades" e
            WHERE lower(e."Nome") = 'cardiologia'
              AND NOT EXISTS (
                  SELECT 1 FROM "Medicos" m
                  WHERE m."EspecialidadeId" = e."Id"
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MedicoHorariosSemanais");

        migrationBuilder.DropColumn(
            name: "OrigemAgendaSemanal",
            table: "Disponibilidades");
    }
}
