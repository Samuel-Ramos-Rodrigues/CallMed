using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class DisponibilidadeConfiguration : IEntityTypeConfiguration<Disponibilidade>
{
    public void Configure(EntityTypeBuilder<Disponibilidade> entity)
    {
        entity.Property(d => d.Data).HasColumnType("date");
        entity.HasIndex(d => new { d.MedicoId, d.Data, d.Horario }).IsUnique();
        entity.HasOne(d => d.Medico)
            .WithMany(m => m.Disponibilidades)
            .HasForeignKey(d => d.MedicoId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(d => d.AgendaExcecao)
            .WithMany()
            .HasForeignKey(d => d.AgendaExcecaoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MedicoHorarioSemanalConfiguration : IEntityTypeConfiguration<MedicoHorarioSemanal>
{
    public void Configure(EntityTypeBuilder<MedicoHorarioSemanal> entity)
    {
        entity.HasIndex(h => new { h.MedicoId, h.DiaSemana, h.Horario }).IsUnique();
        entity.HasOne(h => h.Medico)
            .WithMany(m => m.HorariosSemanais)
            .HasForeignKey(h => h.MedicoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AgendaExcecaoConfiguration : IEntityTypeConfiguration<AgendaExcecao>
{
    public void Configure(EntityTypeBuilder<AgendaExcecao> entity)
    {
        entity.Property(x => x.Data).HasColumnType("date");
        entity.HasIndex(x => new { x.MedicoId, x.Data, x.Ativa });
        entity.HasOne(x => x.Medico)
            .WithMany()
            .HasForeignKey(x => x.MedicoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
