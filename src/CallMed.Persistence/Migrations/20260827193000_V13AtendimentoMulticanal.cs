using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MKSANCrud.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MKSANCrud.Migrations;

[DbContext(typeof(MKSANContext))]
[Migration("20260827193000_V13AtendimentoMulticanal")]
public partial class V13AtendimentoMulticanal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ConversasAtendimento",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PacienteId = table.Column<int>(type: "integer", nullable: true),
                Canal = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: false),
                IdentificadorExterno = table.Column<string>(
                    type: "character varying(320)",
                    maxLength: 320,
                    nullable: false),
                SessionId = table.Column<string>(
                    type: "character varying(160)",
                    maxLength: 160,
                    nullable: false),
                Assunto = table.Column<string>(
                    type: "character varying(300)",
                    maxLength: 300,
                    nullable: true),
                Modo = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: false),
                ResponsavelUsuarioId = table.Column<string>(
                    type: "text",
                    nullable: true),
                Ativa = table.Column<bool>(
                    type: "boolean",
                    nullable: false),
                CriadoEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                AtualizadoEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                UltimaInteracaoEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                AssumidaEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true),
                VisualizadaEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_ConversasAtendimento",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_ConversasAtendimento_Pacientes_PacienteId",
                    column: x => x.PacienteId,
                    principalTable: "Pacientes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);

                table.ForeignKey(
                    name: "FK_ConversasAtendimento_AspNetUsers_ResponsavelUsuarioId",
                    column: x => x.ResponsavelUsuarioId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "MensagensAtendimento",
            columns: table => new
            {
                Id = table.Column<long>(
                    type: "bigint",
                    nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ConversaAtendimentoId = table.Column<long>(
                    type: "bigint",
                    nullable: false),
                Direcao = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: false),
                Autor = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: false),
                Status = table.Column<string>(
                    type: "character varying(20)",
                    maxLength: 20,
                    nullable: false),
                MensagemExternaId = table.Column<string>(
                    type: "character varying(220)",
                    maxLength: 220,
                    nullable: true),
                Texto = table.Column<string>(
                    type: "character varying(5000)",
                    maxLength: 5000,
                    nullable: false),
                Erro = table.Column<string>(
                    type: "character varying(1000)",
                    maxLength: 1000,
                    nullable: true),
                AutorUsuarioId = table.Column<string>(
                    type: "text",
                    nullable: true),
                CriadoEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false),
                EnviadoEm = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_MensagensAtendimento",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_MensagensAtendimento_ConversasAtendimento_ConversaAtendimentoId",
                    column: x => x.ConversaAtendimentoId,
                    principalTable: "ConversasAtendimento",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);

                table.ForeignKey(
                    name: "FK_MensagensAtendimento_AspNetUsers_AutorUsuarioId",
                    column: x => x.AutorUsuarioId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversasAtendimento_Canal_IdentificadorExterno",
            table: "ConversasAtendimento",
            columns: new[]
            {
                "Canal",
                "IdentificadorExterno"
            },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ConversasAtendimento_PacienteId_UltimaInteracaoEm",
            table: "ConversasAtendimento",
            columns: new[]
            {
                "PacienteId",
                "UltimaInteracaoEm"
            });

        migrationBuilder.CreateIndex(
            name: "IX_ConversasAtendimento_ResponsavelUsuarioId",
            table: "ConversasAtendimento",
            column: "ResponsavelUsuarioId");

        migrationBuilder.CreateIndex(
            name: "IX_MensagensAtendimento_ConversaAtendimentoId_CriadoEm",
            table: "MensagensAtendimento",
            columns: new[]
            {
                "ConversaAtendimentoId",
                "CriadoEm"
            });

        migrationBuilder.CreateIndex(
            name: "IX_MensagensAtendimento_ConversaAtendimentoId_MensagemExternaId",
            table: "MensagensAtendimento",
            columns: new[]
            {
                "ConversaAtendimentoId",
                "MensagemExternaId"
            },
            unique: true,
            filter: "\"MensagemExternaId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_MensagensAtendimento_AutorUsuarioId",
            table: "MensagensAtendimento",
            column: "AutorUsuarioId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MensagensAtendimento");

        migrationBuilder.DropTable(
            name: "ConversasAtendimento");
    }
}
