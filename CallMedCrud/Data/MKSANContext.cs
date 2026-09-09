using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Data;

public class MKSANContext : IdentityDbContext<Usuario>
{
    public MKSANContext(DbContextOptions<MKSANContext> options) : base(options)
    {
    }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<Medico> Medicos => Set<Medico>();
    public DbSet<Especialidade> Especialidades => Set<Especialidade>();
    public DbSet<Disponibilidade> Disponibilidades => Set<Disponibilidade>();
    public DbSet<MedicoHorarioSemanal> MedicoHorariosSemanais => Set<MedicoHorarioSemanal>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<ConversaAgente> ConversasAgente => Set<ConversaAgente>();
    public DbSet<MensagemConversaAgente> MensagensAgente => Set<MensagemConversaAgente>();
    public DbSet<ConversaAtendimento> ConversasAtendimento => Set<ConversaAtendimento>();
    public DbSet<MensagemAtendimento> MensagensAtendimento => Set<MensagemAtendimento>();
    public DbSet<ListaEspera> ListasEspera => Set<ListaEspera>();
    public DbSet<AgendaExcecao> AgendaExcecoes => Set<AgendaExcecao>();
    public DbSet<SolicitacaoAtendimento> SolicitacoesAtendimento => Set<SolicitacaoAtendimento>();
    public DbSet<ConvenioEspecialidade> ConveniosEspecialidades => Set<ConvenioEspecialidade>();
    public DbSet<AuditoriaEvento> AuditoriaEventos => Set<AuditoriaEvento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MKSANContext).Assembly);
    }
}
