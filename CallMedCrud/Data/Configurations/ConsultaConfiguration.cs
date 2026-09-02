using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class ConsultaConfiguration : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> entity)
    {
        entity.Property(c => c.Data).HasColumnType("date");

        // Consultas canceladas liberam o slot; qualquer outro estado ocupa a vaga.
        entity.HasIndex(c => new { c.MedicoId, c.Data, c.Horario })
            .HasDatabaseName("IX_Consultas_Slot_Ativo")
            .HasFilter("lower(\"Status\") <> 'cancelada'")
            .IsUnique();

        entity.HasIndex(c => new { c.PacienteId, c.Data });
        entity.HasOne(c => c.Paciente)
            .WithMany(p => p.Consultas)
            .HasForeignKey(c => c.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(c => c.Medico)
            .WithMany(m => m.Consultas)
            .HasForeignKey(c => c.MedicoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
