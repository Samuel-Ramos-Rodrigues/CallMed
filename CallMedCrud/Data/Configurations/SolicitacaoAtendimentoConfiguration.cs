using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data.Configurations;

public sealed class SolicitacaoAtendimentoConfiguration : IEntityTypeConfiguration<SolicitacaoAtendimento>
{
    public void Configure(EntityTypeBuilder<SolicitacaoAtendimento> entity)
    {
        entity.ToTable("SolicitacoesAtendimento");
        entity.Property(x => x.Canal).HasConversion<string>().HasMaxLength(20);
        entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        entity.Property(x => x.DataPreferida).HasColumnType("date");
        entity.HasIndex(x => new { x.Status, x.CriadoEm });
        entity.HasIndex(x => new { x.PacienteId, x.CriadoEm });
        entity.HasOne(x => x.Paciente).WithMany().HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Especialidade).WithMany().HasForeignKey(x => x.EspecialidadeId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Medico).WithMany().HasForeignKey(x => x.MedicoId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Consulta).WithMany().HasForeignKey(x => x.ConsultaId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.ConversaAtendimento).WithMany().HasForeignKey(x => x.ConversaAtendimentoId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.ResponsavelUsuario).WithMany().HasForeignKey(x => x.ResponsavelUsuarioId).OnDelete(DeleteBehavior.SetNull);
    }
}
