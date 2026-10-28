using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class AuditoriaEventoConfiguration : IEntityTypeConfiguration<AuditoriaEvento>
{
    public void Configure(EntityTypeBuilder<AuditoriaEvento> entity)
    {
        entity.ToTable("AuditoriaEventos");
        entity.HasIndex(x => x.CriadoEm);
        entity.HasIndex(x => new { x.Entidade, x.EntidadeId, x.CriadoEm });
        entity.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
