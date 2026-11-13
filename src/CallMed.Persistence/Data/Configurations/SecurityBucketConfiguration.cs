using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
namespace MKSANCrud.Data.Configurations;
public sealed class SecurityBucketConfiguration : IEntityTypeConfiguration<SecurityBucket>
{
    public void Configure(EntityTypeBuilder<SecurityBucket> b)
    {
        b.ToTable("SecurityBuckets"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasMaxLength(64);
        b.HasIndex(x => x.ExpiraEm);
    }
}

public sealed class AtendimentoLeaseConfiguration : IEntityTypeConfiguration<AtendimentoLeaseRow> { public void Configure(EntityTypeBuilder<AtendimentoLeaseRow> b) { b.ToTable("AtendimentoLeases"); b.HasKey(x => x.ConversaId); } }
