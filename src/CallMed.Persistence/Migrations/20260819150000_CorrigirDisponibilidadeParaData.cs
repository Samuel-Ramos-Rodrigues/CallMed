using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MKSANCrud.Data;

#nullable disable

namespace MKSANCrud.Migrations
{
    [DbContext(typeof(MKSANContext))]
    [Migration("20260819150000_CorrigirDisponibilidadeParaData")]
    public partial class CorrigirDisponibilidadeParaData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DiaSemana",
                table: "Disponibilidades",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "Data",
                table: "Disponibilidades",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disponibilidades_MedicoId_Data_Horario",
                table: "Disponibilidades",
                columns: new[] { "MedicoId", "Data", "Horario" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Disponibilidades_MedicoId_Data_Horario",
                table: "Disponibilidades");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "Disponibilidades");

            migrationBuilder.Sql("UPDATE \"Disponibilidades\" SET \"DiaSemana\" = 'Não definido' WHERE \"DiaSemana\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "DiaSemana",
                table: "Disponibilidades",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
