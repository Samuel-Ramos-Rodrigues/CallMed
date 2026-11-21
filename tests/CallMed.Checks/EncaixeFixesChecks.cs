using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;

internal static class EncaixeFixesChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        foreach (var desativarPaciente in new[] { false, true })
        {
            await using var db = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>()
                .UseInMemoryDatabase("encaixe-fixes-" + Guid.NewGuid()).Options);
            var clock = new ClinicaClock();
            var dia = clock.Hoje.AddDays(2);
            db.Especialidades.Add(new() { Id = 1, Nome = "Clínica fictícia" });
            db.Medicos.Add(new() { Id = 1, Nome = "Profissional fictício", EspecialidadeId = 1 });
            db.Pacientes.AddRange(
                new() { Id = 1, Nome = "Paciente fictício A", Cpf = "52998224725" },
                new() { Id = 2, Nome = "Paciente fictício B", Cpf = "11144477735" });
            db.ListasEspera.AddRange(
                new() { Id = 1, PacienteId = 1, MedicoId = 1, Prioridade = 10 },
                new() { Id = 2, PacienteId = 2, MedicoId = 1, Prioridade = 5 });
            db.Disponibilidades.Add(new() { MedicoId = 1, Data = dia, Horario = "10:00", Ativo = true });
            await db.SaveChangesAsync();
            var envio = new AtendimentoEnvioService([], db, TimeProvider.System,
                NullLogger<AtendimentoEnvioService>.Instance, Options.Create(new GmailInboxOptions()));
            var motor = new EncaixeService(db, clock,
                new ConvenioElegibilidadeService(db, new ConvenioService(clock)),
                new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db)), envio,
                new ConfigurationBuilder().Build());
            check(await motor.ReservarAsync() == 1, "Encaixe: cria oferta para o primeiro candidato");
            var antiga = await db.Ofertas.SingleAsync();
            antiga.Tentativas = 5; // a limpeza não depende da tentativa de notificação
            if (desativarPaciente) (await db.Pacientes.FindAsync(1))!.Ativo = false;
            else (await db.ListasEspera.FindAsync(1))!.Ativa = false;
            await db.SaveChangesAsync();
            var novas = await motor.ReservarAsync();
            check(novas == 1 && antiga.Estado == "Revogada" &&
                  await db.Ofertas.AnyAsync(o => o.Estado == "Aberta" && o.ListaEsperaId == 2),
                desativarPaciente
                    ? "Encaixe: paciente inativo libera vaga mesmo após cinco tentativas"
                    : "Encaixe: pedido inativo libera vaga mesmo após cinco tentativas");
            check(await motor.ReservarAsync() == 0 && await db.Ofertas.CountAsync(o => o.Estado == "Aberta") == 1,
                "Encaixe: reprocessamento mantém uma única oferta ativa");
        }
    }
}
