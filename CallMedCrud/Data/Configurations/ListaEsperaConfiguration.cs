using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class ListaEsperaConfiguration : IEntityTypeConfiguration<ListaEspera>
{
    public void Configure(EntityTypeBuilder<ListaEspera> entity)
    {
        entity.Property(x => x.DataPreferida).HasColumnType("date");
        entity.HasIndex(x => new { x.Ativa, x.CriadoEm });
        entity.HasOne(x => x.Paciente)
            .WithMany()
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Medico)
            .WithMany()
            .HasForeignKey(x => x.MedicoId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Especialidade)
            .WithMany()
            .HasForeignKey(x => x.EspecialidadeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
