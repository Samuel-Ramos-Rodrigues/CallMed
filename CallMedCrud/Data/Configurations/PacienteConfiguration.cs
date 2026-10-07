using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class PacienteConfiguration : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> entity)
    {
        entity.HasIndex(p => p.Cpf).IsUnique();
        entity.HasIndex(p => p.Email).IsUnique();
        entity.HasIndex(p => p.UsuarioId).IsUnique();
        entity.Property(p => p.DataNascimento).HasColumnType("date");
        entity.Property(p => p.ValidadeConvenio).HasColumnType("date");
        entity.HasOne(p => p.Usuario)
            .WithOne()
            .HasForeignKey<Paciente>(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
