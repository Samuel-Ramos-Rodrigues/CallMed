using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class MedicoConfiguration : IEntityTypeConfiguration<Medico>
{
    public void Configure(EntityTypeBuilder<Medico> entity)
    {
        entity.HasIndex(m => m.Crm).IsUnique();
        entity.HasIndex(m => m.UsuarioId)
            .IsUnique()
            .HasFilter("\"UsuarioId\" IS NOT NULL");
        entity.HasIndex(m => m.Email)
            .IsUnique()
            .HasFilter("\"Email\" IS NOT NULL");
        entity.HasOne(m => m.Usuario)
            .WithOne()
            .HasForeignKey<Medico>(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(m => m.EspecialidadeCadastro)
            .WithMany(e => e.Medicos)
            .HasForeignKey(m => m.EspecialidadeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
