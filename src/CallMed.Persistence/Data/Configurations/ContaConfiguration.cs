using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;

namespace MKSANCrud.Data.Configurations;
public sealed class ContaPerfilConfiguration : IEntityTypeConfiguration<ContaPerfil>
{
    public void Configure(EntityTypeBuilder<ContaPerfil> b)
    {
        b.ToTable("ContaPerfis"); b.HasKey(x => x.UsuarioId);
        b.Property(x => x.NomeExibicao).HasMaxLength(150);
        b.Property(x => x.Telefone).HasMaxLength(30); b.Property(x => x.Tema).HasMaxLength(12);
        b.HasOne<Usuario>().WithOne().HasForeignKey<ContaPerfil>(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class ContaFotoConfiguration : IEntityTypeConfiguration<ContaFoto>
{
    public void Configure(EntityTypeBuilder<ContaFoto> b)
    {
        b.ToTable("ContaFotos"); b.HasKey(x => x.UsuarioId); b.Property(x => x.Tipo).HasMaxLength(20);
        b.HasOne<Usuario>().WithOne().HasForeignKey<ContaFoto>(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class ContaSessaoConfiguration : IEntityTypeConfiguration<ContaSessao>
{
    public void Configure(EntityTypeBuilder<ContaSessao> b)
    {
        b.ToTable("ContaSessoes"); b.HasKey(x => x.Id); b.Property(x => x.RefreshVersion).IsConcurrencyToken(); b.Property(x => x.Dispositivo).HasMaxLength(80);
        b.HasIndex(x => new { x.UsuarioId, x.CriadoEm });
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class ContaEventoConfiguration : IEntityTypeConfiguration<ContaEvento>
{
    public void Configure(EntityTypeBuilder<ContaEvento> b)
    {
        b.ToTable("ContaEventos"); b.HasKey(x => x.Id); b.Property(x => x.Descricao).HasMaxLength(160);
        b.HasIndex(x => new { x.UsuarioId, x.CriadoEm });
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
