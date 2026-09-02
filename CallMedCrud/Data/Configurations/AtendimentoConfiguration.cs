using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class ConversaAtendimentoConfiguration : IEntityTypeConfiguration<ConversaAtendimento>
{
    public void Configure(EntityTypeBuilder<ConversaAtendimento> entity)
    {
        entity.ToTable("ConversasAtendimento");
        entity.Property(c => c.Canal).HasConversion<string>().HasMaxLength(20);
        entity.Property(c => c.Modo).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(c => new { c.Canal, c.IdentificadorExterno }).IsUnique();
        entity.HasIndex(c => new { c.PacienteId, c.UltimaInteracaoEm });
        entity.HasOne(c => c.Paciente)
            .WithMany()
            .HasForeignKey(c => c.PacienteId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(c => c.ResponsavelUsuario)
            .WithMany()
            .HasForeignKey(c => c.ResponsavelUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MensagemAtendimentoConfiguration : IEntityTypeConfiguration<MensagemAtendimento>
{
    public void Configure(EntityTypeBuilder<MensagemAtendimento> entity)
    {
        entity.ToTable("MensagensAtendimento");
        entity.Property(m => m.Direcao).HasConversion<string>().HasMaxLength(20);
        entity.Property(m => m.Autor).HasConversion<string>().HasMaxLength(20);
        entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        entity.HasIndex(m => new { m.ConversaAtendimentoId, m.CriadoEm });
        entity.HasIndex(m => new { m.ConversaAtendimentoId, m.MensagemExternaId })
            .IsUnique()
            .HasFilter("\"MensagemExternaId\" IS NOT NULL");
        entity.HasOne(m => m.Conversa)
            .WithMany(c => c.Mensagens)
            .HasForeignKey(m => m.ConversaAtendimentoId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(m => m.AutorUsuario)
            .WithMany()
            .HasForeignKey(m => m.AutorUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
