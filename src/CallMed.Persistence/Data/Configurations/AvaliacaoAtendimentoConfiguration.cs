using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class AvaliacaoAtendimentoConfiguration : IEntityTypeConfiguration<AvaliacaoAtendimento>
{
    public void Configure(EntityTypeBuilder<AvaliacaoAtendimento> b)
    {
        b.ToTable("AvaliacoesAtendimento", t => t.HasCheckConstraint("CK_Avaliacao_Nota", "\"Nota\" IS NULL OR \"Nota\" BETWEEN 1 AND 5"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Comentario).HasMaxLength(1000);
        b.Property(x => x.RespostaExternaId).HasMaxLength(220);
        b.Property(x => x.Versao).IsConcurrencyToken();
        b.HasIndex(x => new { x.ConversaAtendimentoId, x.UltimaEntradaId }).IsUnique();
        b.HasIndex(x => x.CriadoEm);
        b.HasOne(x => x.Conversa).WithMany().HasForeignKey(x => x.ConversaAtendimentoId).OnDelete(DeleteBehavior.Cascade);
    }
}
