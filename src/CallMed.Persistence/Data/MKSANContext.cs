using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data;

public class MKSANContext : IdentityDbContext<Usuario>, IDataProtectionKeyContext
{
    public MKSANContext(DbContextOptions<MKSANContext> options) : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<ContaPerfil> ContaPerfis => Set<ContaPerfil>();
    public DbSet<ContaFoto> ContaFotos => Set<ContaFoto>();
    public DbSet<ContaSessao> ContaSessoes => Set<ContaSessao>();
    public DbSet<ContaEvento> ContaEventos => Set<ContaEvento>();
    public DbSet<SecurityBucket> SecurityBuckets => Set<SecurityBucket>();
    public DbSet<AtendimentoLeaseRow> AtendimentoLeases => Set<AtendimentoLeaseRow>();
    public DbSet<ExameHistorico> ExamesHistorico => Set<ExameHistorico>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<ProfissionalSaude> Profissionais => Set<ProfissionalSaude>();
    public DbSet<ProfissionalSaude> Psicologos => Profissionais;
    // Alias do repositório legado: mesmo tipo e mesma tabela, sem duplicar registros.
    public DbSet<Psicologo> Medicos => Psicologos;
    public DbSet<Especialidade> Especialidades => Set<Especialidade>();
    public DbSet<Disponibilidade> Disponibilidades => Set<Disponibilidade>();
    public DbSet<MedicoHorarioSemanal> MedicoHorariosSemanais => Set<MedicoHorarioSemanal>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<ConversaAgente> ConversasAgente => Set<ConversaAgente>();
    public DbSet<MensagemConversaAgente> MensagensAgente => Set<MensagemConversaAgente>();
    public DbSet<ConversaAtendimento> ConversasAtendimento => Set<ConversaAtendimento>();
    public DbSet<AvaliacaoAtendimento> AvaliacoesAtendimento => Set<AvaliacaoAtendimento>();
    public DbSet<MensagemAtendimento> MensagensAtendimento => Set<MensagemAtendimento>();
    public DbSet<ListaEspera> ListasEspera => Set<ListaEspera>();
    public DbSet<AgendaExcecao> AgendaExcecoes => Set<AgendaExcecao>();
    public DbSet<SolicitacaoAtendimento> SolicitacoesAtendimento => Set<SolicitacaoAtendimento>();
    public DbSet<ConvenioEspecialidade> ConveniosEspecialidades => Set<ConvenioEspecialidade>();
    public DbSet<AuditoriaEvento> AuditoriaEventos => Set<AuditoriaEvento>();

    public DbSet<GuardaRegistroPsicologico> GuardasRegistrosPsicologicos => Set<GuardaRegistroPsicologico>();
    public DbSet<RegistroPsicologico> RegistrosPsicologicos => Set<RegistroPsicologico>();
    public DbSet<AutorizacaoResponsavel> AutorizacoesResponsaveis => Set<AutorizacaoResponsavel>();
    public DbSet<Jornada> Jornadas => Set<Jornada>();
    public DbSet<SalaAtendimento> Salas => Set<SalaAtendimento>();
    public DbSet<BloqueioSala> BloqueiosSala => Set<BloqueioSala>();
    public DbSet<SessaoTele> SessoesTele => Set<SessaoTele>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();
    public DbSet<ChaveClinica> ChavesClinicas => Set<ChaveClinica>();
    public DbSet<FormularioClinico> FormulariosClinicos => Set<FormularioClinico>();
    public DbSet<RespostaClinica> RespostasClinicas => Set<RespostaClinica>();
    public DbSet<SinalTele> SinaisTele => Set<SinalTele>();
    public DbSet<LinkConsulta> LinksConsulta => Set<LinkConsulta>();
    public DbSet<MobileTicket> MobileTickets => Set<MobileTicket>();
    public DbSet<MedicaoPiloto> MedicoesPiloto => Set<MedicaoPiloto>();
    public DbSet<MedicaoOperacional> MedicoesOperacionais => Set<MedicaoOperacional>();
    public DbSet<SaudeRotina> SaudeRotinas => Set<SaudeRotina>();
    public DbSet<PedidoPrivacidade> PedidosPrivacidade => Set<PedidoPrivacidade>();
    public DbSet<PepEvento> PepEventos => Set<PepEvento>();
    private void PrepararOutbox() {
        if (ChangeTracker.Entries<MedicaoPiloto>().Any(x=>x.State is EntityState.Deleted or EntityState.Modified)) throw new InvalidOperationException("Medições do piloto são imutáveis; registre uma revisão.");
        if (ChangeTracker.Entries<RegistroPsicologico>().Any(x=>x.State is EntityState.Deleted or EntityState.Modified)) throw new InvalidOperationException("Registros psicológicos publicados são imutáveis; publique uma retificação.");
        foreach(var e in ChangeTracker.Entries<Consulta>().Where(x=>x.State==EntityState.Added || (x.State==EntityState.Modified && (x.Property(c=>c.Status).IsModified || x.Property(c=>c.Data).IsModified || x.Property(c=>c.Horario).IsModified || x.Property(c=>c.MedicoId).IsModified || x.Property(c=>c.PacienteId).IsModified))).ToArray()) {
            if(!ChangeTracker.Entries<PepEvento>().Any(x=>x.State==EntityState.Added && ReferenceEquals(x.Entity.Consulta,e.Entity))) PepEventos.Add(new(){Consulta=e.Entity,PacienteId=e.Entity.PacienteId,MedicoId=e.Entity.MedicoId,Estado=e.Entity.Status,Data=e.Entity.Data,Horario=e.Entity.Horario});
        }
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct=default) {PrepararOutbox();return base.SaveChangesAsync(ct);}
    public override int SaveChanges() {PrepararOutbox();return base.SaveChanges();}
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MedicaoPiloto>().Property(x=>x.Data).HasColumnType("date");
        modelBuilder.Entity<MedicaoPiloto>().Property(x=>x.CustoHoraRecepcao).HasPrecision(12,2);
        modelBuilder.Entity<MedicaoPiloto>().HasIndex(x=>new{x.Periodo,x.Data,x.Revisao}).IsUnique();
        modelBuilder.Entity<PedidoPrivacidade>().Property(x=>x.Versao).IsConcurrencyToken();
        modelBuilder.Entity<PedidoPrivacidade>().HasIndex(x=>new{x.Estado,x.CriadoEm});
        modelBuilder.Entity<PedidoPrivacidade>().HasIndex(x=>x.UsuarioId);
        modelBuilder.Entity<GuardaRegistroPsicologico>().HasOne(x=>x.Registro).WithMany().HasForeignKey(x=>x.RegistroId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GuardaRegistroPsicologico>().HasIndex(x=>new{x.RegistroId,x.ChaveId}).IsUnique();
        modelBuilder.Entity<GuardaRegistroPsicologico>().HasOne<Psicologo>().WithMany().HasForeignKey(x=>x.PsicologoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GuardaRegistroPsicologico>().HasOne<ChaveClinica>().WithMany().HasForeignKey(x=>x.ChaveId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AutorizacaoResponsavel>().HasOne<Paciente>().WithMany().HasForeignKey(x=>x.PacienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AutorizacaoResponsavel>().HasOne<Psicologo>().WithMany().HasForeignKey(x=>x.PsicologoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistroPsicologico>().HasOne<RegistroPsicologico>().WithMany().HasForeignKey(x=>x.RetificaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistroPsicologico>().HasIndex(x=>new{x.ConsultaId,x.CriadoEm});
        modelBuilder.Entity<RegistroPsicologico>().HasIndex(x=>x.RetificaId).IsUnique().HasFilter("\"RetificaId\" IS NOT NULL");
        modelBuilder.Entity<RegistroPsicologico>().HasOne<Consulta>().WithMany().HasForeignKey(x=>x.ConsultaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistroPsicologico>().HasOne<Paciente>().WithMany().HasForeignKey(x=>x.PacienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistroPsicologico>().HasOne<Psicologo>().WithMany().HasForeignKey(x=>x.PsicologoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AutorizacaoResponsavel>().HasIndex(x=>new{x.PacienteId,x.PsicologoId});
        modelBuilder.Entity<Jornada>().HasKey(x=>x.ConsultaId);
        modelBuilder.Entity<Jornada>().HasOne(x=>x.Consulta).WithOne().HasForeignKey<Jornada>(x=>x.ConsultaId).OnDelete(DeleteBehavior.Cascade);
        // O nome CLR específico evita conflito com Sala.cs de versões anteriores.
        // A tabela permanece a mesma; esta correção não altera o esquema nem os dados.
        modelBuilder.Entity<SalaAtendimento>().ToTable("Salas");
        modelBuilder.Entity<SalaAtendimento>().Property(x=>x.Localizacao).HasMaxLength(120);
        modelBuilder.Entity<SalaAtendimento>().Property(x=>x.Recursos).HasMaxLength(500);
        modelBuilder.Entity<BloqueioSala>().HasOne(x=>x.Sala).WithMany().HasForeignKey(x=>x.SalaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BloqueioSala>().Property(x=>x.Inicio).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<BloqueioSala>().Property(x=>x.Fim).HasColumnType("timestamp without time zone");
        modelBuilder.Entity<BloqueioSala>().Property(x=>x.Motivo).HasMaxLength(200);
        modelBuilder.Entity<BloqueioSala>().HasIndex(x=>new{x.SalaId,x.Inicio,x.Fim});
        modelBuilder.Entity<SessaoTele>().HasOne<Consulta>().WithMany().HasForeignKey(x=>x.ConsultaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SessaoTele>().Property(x=>x.UsuarioId).HasMaxLength(450);
        modelBuilder.Entity<SessaoTele>().Property(x=>x.Papel).HasMaxLength(20);
        modelBuilder.Entity<SessaoTele>().Property(x=>x.Snapshot).HasMaxLength(180);
        modelBuilder.Entity<SessaoTele>().Property(x=>x.Estado).HasMaxLength(20);
        modelBuilder.Entity<SessaoTele>().Property(x=>x.MotivoSaida).HasMaxLength(80);
        modelBuilder.Entity<SessaoTele>().HasIndex(x=>new{x.ConsultaId,x.EntradaEm});
        modelBuilder.Entity<SessaoTele>().HasIndex(x=>new{x.UsuarioId,x.UltimoSinalEm});

        modelBuilder.Entity<Jornada>().HasOne(x=>x.Sala).WithMany().HasForeignKey(x=>x.SalaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Oferta>().HasOne(x=>x.ListaEspera).WithMany().HasForeignKey(x=>x.ListaEsperaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Oferta>().HasIndex(x=>new{x.MedicoId,x.Data,x.Horario}).IsUnique().HasFilter("\"Estado\" = 'Aberta'");
        modelBuilder.Entity<PepEvento>().HasOne(x=>x.Consulta).WithMany().HasForeignKey(x=>x.ConsultaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Oferta>().Property(x=>x.Data).HasColumnType("date");
        modelBuilder.Entity<MedicaoOperacional>().Property(x=>x.Data).HasColumnType("date");
        modelBuilder.Entity<PepEvento>().Property(x=>x.Data).HasColumnType("date");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MKSANContext).Assembly);
    }
}
