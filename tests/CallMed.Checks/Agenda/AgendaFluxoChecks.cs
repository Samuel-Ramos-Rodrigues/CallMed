using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

internal static class AgendaFluxoChecks
{
    private sealed class Relogio : IClinicaClock
    {
        public DateTime Agora => new(2030, 5, 20, 14, 0, 0);
        public DateTime Hoje => Agora.Date;
        public DateTime ConverterUtc(DateTime valor) => valor;
        public DateTime ConverterParaUtc(DateTime valor) => valor;
    }

    private static ControllerContext Perfil(string id, string papel)
    {
        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Role, papel)], "Teste");
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new(identidade) } };
    }

    public static async Task RunAsync(Action<bool, string> check)
    {
        var clock = new Relogio();
        var servicos = new ServiceCollection();
        var banco = Guid.NewGuid().ToString();
        servicos.AddLogging();
        servicos.AddDbContext<MKSANContext>(o => o.UseInMemoryDatabase(banco)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        servicos.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<MKSANContext>();
        await using var provedor = servicos.BuildServiceProvider();
        using var escopo = provedor.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<MKSANContext>();
        var convenios = new ConvenioService(clock);
        var agenda = new AgendamentoService(db, clock, new(db), convenios, new(db, convenios),
            NullLogger<AgendamentoService>.Instance);
        var vinculos = new UsuarioVinculoService(db, escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>());
        var dia = clock.Hoje.AddDays(1);
        db.Users.AddRange(new Usuario { Id = "p", UserName = "paciente@example.invalid" },
            new Usuario { Id = "g", UserName = "profissional@example.invalid" });
        db.Pacientes.AddRange(new Paciente { Id = 1, Nome = "Paciente A", UsuarioId = "p", Cpf = "52998224725" },
            new Paciente { Id = 2, Nome = "Paciente B", Cpf = "11144477735" });
        db.Especialidades.Add(new() { Id = 1, Nome = "Clínica" });
        db.Medicos.AddRange(new ProfissionalSaude { Id = 1, Nome = "Profissional A", UsuarioId = "g",
                EspecialidadeId = 1, DuracaoPadraoMinutos = 30 },
            new ProfissionalSaude { Id = 2, Nome = "Profissional B", EspecialidadeId = 1, DuracaoPadraoMinutos = 30 });
        Consulta Consulta(int id, int paciente, DateTime data, string hora, int duracao, string estado) =>
            new() { Id = id, PacienteId = paciente, MedicoId = 1, Data = data, Horario = hora, DuracaoMinutos = duracao, Status = estado };
        db.Consultas.AddRange(Consulta(101, 1, clock.Hoje, "09:00", 30, ConsultaStatus.Realizada),
            Consulta(102, 1, clock.Hoje, "16:00", 30, ConsultaStatus.Confirmada),
            Consulta(103, 1, dia, "10:00", 60, ConsultaStatus.Confirmada),
            Consulta(104, 2, dia, "12:30", 30, ConsultaStatus.Confirmada));
        db.Jornadas.Add(new() { ConsultaId = 103, Duracao = 60 });
        foreach (var hora in new[] { "10:30", "11:00", "12:00", "15:00" })
            foreach (var medico in new[] { 1, 2 })
                db.Disponibilidades.Add(new() { MedicoId = medico, Data = dia, Horario = hora, Ativo = true });
        await db.SaveChangesAsync();

        var home = new HomeController(db, clock, vinculos) { ControllerContext = Perfil("p", "Paciente") };
        var inicio = (HomeViewModel)((ViewResult)await home.Index()).Model!;
        check(inicio.ProximasConsultas.First().Id == 102, "Home destaca a consulta de hoje que ainda ocorrerá");
        var painel = new MedicoPainelController(db, vinculos, clock, agenda, new(db, clock)) { ControllerContext = Perfil("g", "Medico") };
        var modelo = (MedicoPainelViewModel)((ViewResult)await painel.Index(default)).Model!;
        check(!modelo.ProximasVagasLivres.Any(x => x.Horario == "10:30") &&
              modelo.ProximasVagasLivres.Any(x => x.Horario == "11:00"), "Painel exibe a primeira vaga após o intervalo ocupado");
        check(!(await agenda.RemarcarAsync(103, dia, "12:00")).Sucesso &&
            (await db.Consultas.FindAsync(103))!.Horario == "10:00", "Remarcação mantém a duração de 60 minutos e preserva a reserva em conflito");
        check(!(await agenda.AgendarAsync(1, 2, dia, "10:30", null)).Sucesso,
            "Outro profissional não pode agendar o paciente no meio da mesma consulta");
        check((await agenda.AgendarAsync(1, 2, dia, "11:00", null)).Sucesso,
            "O próximo intervalo pode começar imediatamente ao fim da consulta");

        var ofertaId = Guid.NewGuid();
        db.ListasEspera.Add(new() { Id = 8, PacienteId = 1, MedicoId = 2, Ativa = true });
        db.Ofertas.Add(new() { Id = ofertaId, ListaEsperaId = 8, MedicoId = 2, Data = dia, Horario = "15:00",
            Estado = "Aberta", ExpiraEm = DateTime.UtcNow.AddHours(1) });
        await db.SaveChangesAsync();
        check(!(await agenda.AgendarAsync(1, 2, dia, "15:00", null, ofertaId: ofertaId)).Sucesso &&
            (await db.Ofertas.FindAsync(ofertaId))!.Estado == "Aberta", "Aceite sem modalidade preserva a oferta");
        var video = await agenda.AgendarAsync(1, 2, dia, "15:00", null, ofertaId: ofertaId, modalidade: "Online");
        check(video.Sucesso && await db.Jornadas.AnyAsync(j => j.ConsultaId == video.Consulta!.Id && j.Tele),
            "Aceite por vídeo cria uma jornada de teleconsulta");
    }
}
