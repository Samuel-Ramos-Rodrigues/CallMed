using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class FuncionarioConfiguration : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> entity)
    {
        entity.HasIndex(f => f.Email).IsUnique();
        entity.HasIndex(f => f.UsuarioId).IsUnique();
        entity.HasOne(f => f.Usuario)
            .WithOne()
            .HasForeignKey<Funcionario>(f => f.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
