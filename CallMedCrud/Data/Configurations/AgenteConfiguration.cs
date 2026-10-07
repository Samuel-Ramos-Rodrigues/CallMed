using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class ConversaAgenteConfiguration : IEntityTypeConfiguration<ConversaAgente>
{
    public void Configure(EntityTypeBuilder<ConversaAgente> entity)
    {
        entity.ToTable("ConversasAgente");
        entity.HasIndex(c => new { c.UsuarioId, c.SessionId }).IsUnique();
        entity.HasOne(c => c.Usuario)
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MensagemConversaAgenteConfiguration : IEntityTypeConfiguration<MensagemConversaAgente>
{
    public void Configure(EntityTypeBuilder<MensagemConversaAgente> entity)
    {
        entity.ToTable("MensagensAgente");
        entity.HasIndex(m => new { m.ConversaAgenteId, m.CriadoEm });
        entity.HasOne(m => m.Conversa)
            .WithMany(c => c.Mensagens)
            .HasForeignKey(m => m.ConversaAgenteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
