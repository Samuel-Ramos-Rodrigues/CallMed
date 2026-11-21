using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;

namespace MKSANCrud.Data.Configurations;

public sealed class ExameHistoricoConfiguration : IEntityTypeConfiguration<ExameHistorico>
{
    public void Configure(EntityTypeBuilder<ExameHistorico> entity)
    {
        entity.ToTable("ExamesHistorico");
        entity.Property(x => x.DataRealizacao).HasColumnType("date");
        entity.Property(x => x.StatusTriagem).HasMaxLength(20).HasDefaultValue(ExameTriagemStatus.Pendente);
        entity.Property(x => x.TriadoPorUsuarioId).HasMaxLength(450);
        entity.Property(x => x.ObservacaoTriagem).HasMaxLength(600);
        entity.HasIndex(x => new { x.PacienteId, x.DataRealizacao });
        entity.HasIndex(x => new { x.StatusTriagem, x.Ativo, x.CriadoEm });
        entity.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
    }
}
