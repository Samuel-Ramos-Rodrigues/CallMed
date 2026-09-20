using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class ConvenioEspecialidadeConfiguration : IEntityTypeConfiguration<ConvenioEspecialidade>
{
    public void Configure(EntityTypeBuilder<ConvenioEspecialidade> entity)
    {
        entity.ToTable("ConveniosEspecialidades");
        entity.HasIndex(x => new { x.ConvenioChave, x.EspecialidadeId }).IsUnique();
        entity.HasOne(x => x.Especialidade)
            .WithMany()
            .HasForeignKey(x => x.EspecialidadeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
