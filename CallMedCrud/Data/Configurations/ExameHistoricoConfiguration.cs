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
        entity.HasIndex(x => new { x.PacienteId, x.DataRealizacao });
        entity.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
    }
}
