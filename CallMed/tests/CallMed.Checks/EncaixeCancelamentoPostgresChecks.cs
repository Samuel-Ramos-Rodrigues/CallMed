using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;
using Npgsql;

/// <summary>
/// Exercita agendamento -> cancelamento -> oferta automática -> tarefa de
/// notificação -> aceite -> corrida, usando PostgreSQL real e dados sintéticos.
/// Nunca conectar ao banco de produção: o banco dedicado é apagado no início.
/// </summary>
internal static class EncaixeCancelamentoPostgresChecks
{
    private const string NomeBanco = "callmed_test_encaixe";

    public static async Task RunAsync(Action<bool, string> check)
    {
        var connection = Environment.GetEnvironmentVariable("CALLMED_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Defina CALLMED_TEST_DB para um PostgreSQL LOCAL isolado.");

        var cs = new NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(cs.Host, "127.0.0.1", StringComparison.Ordinal) ||
            !string.Equals(cs.Database, NomeBanco, StringComparison.Ordinal) ||
            cs.Multiplexing)
            throw new InvalidOperationException(
                "Teste bloqueado: somente Host=127.0.0.1;Database=callmed_test_encaixe, sem multiplexing.");

        if (Environment.GetEnvironmentVariable("CALLMED_TEST_RESET") != "SIM")
            throw new InvalidOperationException(
                "O teste APAGA o banco local callmed_test_encaixe. Para autorizar, defina CALLMED_TEST_RESET=SIM.");

        MKSANContext Db() => new(new DbContextOptionsBuilder<MKSANContext>().UseNpgsql(connection).Options);
        var clock = new ClinicaClock();
        var day = clock.Hoje.AddDays(3);
        const string horario = "10:00";

        EncaixeService Motor(MKSANContext db)
        {
            var convenio = new ConvenioService(clock);
            var envio = new AtendimentoEnvioService(
                [], db, TimeProvider.System,
                NullLogger<AtendimentoEnvioService>.Instance,
                Options.Create(new GmailInboxOptions()));
            return new EncaixeService(
                db, clock, new ConvenioElegibilidadeService(db, convenio),
                new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db)),
                envio, new ConfigurationBuilder().Build());
        }

        AgendamentoService Agenda(MKSANContext db, EncaixeService? motor = null)
        {
            var convenio = new ConvenioService(clock);
            return new AgendamentoService(
                db, clock, new EspecialidadeService(db), convenio,
                new ConvenioElegibilidadeService(db, convenio),
                NullLogger<AgendamentoService>.Instance, motor);
        }

        // O reset é autorizado SOMENTE para o nome e host verificados acima.
        await using (var db = Db())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            db.Especialidades.Add(new Especialidade { Id = 1, Nome = "Clínica fictícia" });
            db.Medicos.Add(new Medico
            {
                Id = 1, Nome = "Profissional fictício", EspecialidadeId = 1,
                DuracaoPadraoMinutos = 30
            });
            db.Pacientes.AddRange(
                new Paciente { Id = 1, Nome = "Paciente A fictício", Cpf = "52998224725", Telefone = "11990000001" },
                new Paciente { Id = 2, Nome = "Paciente B fictício", Cpf = "11144477735", Telefone = "11990000002" },
                new Paciente { Id = 3, Nome = "Paciente C fictício", Cpf = "12345678909", Telefone = "11990000003" },
                new Paciente { Id = 4, Nome = "Paciente D fictício", Cpf = "98765432100", Telefone = "11990000004" });
            db.Disponibilidades.Add(new Disponibilidade
            {
                MedicoId = 1, Data = day, Horario = horario, Ativo = true
            });
            await db.SaveChangesAsync();

            // Criação da primeira consulta usa o serviço real de agendamento.
            var original = await Agenda(db).AgendarAsync(1, 1, day, horario, "Teste sintético");
            check(original.Sucesso && original.Consulta is not null,
                "ENCAIXE-PG: consulta inicial agendada pelo serviço real");
            int originalId = original.Consulta!.Id;

            db.ListasEspera.AddRange(
                new ListaEspera { PacienteId = 2, MedicoId = 1, DataPreferida = day, Periodo = "Qualquer", Prioridade = 10 },
                new ListaEspera { PacienteId = 3, MedicoId = 1, DataPreferida = day, Periodo = "Qualquer", Prioridade = 5 },
                new ListaEspera { PacienteId = 4, MedicoId = 1, DataPreferida = day, Periodo = "Qualquer", Prioridade = 1 });
            await db.SaveChangesAsync();

            // INTEGRAÇÃO: a própria chamada CancelarAsync aciona o motor de encaixe.
            var cancelamento = await Agenda(db, Motor(db)).CancelarAsync(originalId);
            check(cancelamento.Sucesso &&
                  await db.Consultas.AsNoTracking().AnyAsync(c => c.Id == originalId && c.Status == ConsultaStatus.Cancelada),
                "ENCAIXE-PG: cancelamento real libera a consulta original");

            var ofertas = await db.Ofertas.AsNoTracking().Include(o => o.ListaEspera)
                .Where(o => o.MedicoId == 1 && o.Data == day && o.Horario == horario && o.Estado == "Aberta")
                .ToListAsync();
            check(ofertas.Count == 1 && ofertas[0].ListaEspera.PacienteId == 2 &&
                  ofertas[0].ExpiraEm > DateTime.UtcNow,
                "ENCAIXE-PG: cancelamento dispara oferta única ao candidato prioritário");
            var ofertaId = ofertas.Single().Id;

            var reaplicacao = await Motor(db).ReservarAsync();
            check(reaplicacao == 0 && await db.Ofertas.CountAsync(o => o.Id == ofertaId) == 1,
                "ENCAIXE-PG: reprocessar não duplica oferta ativa");

            // Não há transportes configurados: a plataforma gera tarefa para
            // a recepção, sem enviar WhatsApp/SMS/e-mail reais.
            await Motor(db).ProcessarAsync(default);
            check(await db.SolicitacoesAtendimento.AsNoTracking().AnyAsync(s =>
                    s.PacienteId == 2 && s.Canal == CanalAtendimento.Telefone &&
                    s.Observacao != null && s.Observacao.Contains(ofertaId.ToString())),
                "ENCAIXE-PG: sem provedor externo, oferta gera tarefa rastreável para recepção");
        }

        var largada = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ResultadoOperacaoConsulta> Reservar(int pacienteId, Guid? ofertaId)
        {
            await largada.Task;
            await using var db = Db();
            return await Agenda(db).AgendarAsync(pacienteId, 1, day, horario,
                "Teste concorrente sintético", ofertaId: ofertaId);
        }

        // Duas conexões/contextos PostgreSQL independentes competem pelo mesmo slot.
        Guid idOferta;
        await using (var db = Db())
            idOferta = await db.Ofertas.Where(x => x.Data == day && x.Horario == horario)
                .Select(x => x.Id).SingleAsync();

        var aceite = Reservar(2, idOferta);
        var tentativaComum = Reservar(3, null);
        largada.SetResult(true);
        var concorrencia = await Task.WhenAll(aceite, tentativaComum);
        check(concorrencia[0].Sucesso && !concorrencia[1].Sucesso,
            "ENCAIXE-PG: aceite da oferta vence tentativa concorrente de agendamento comum");
        int consultaNova = concorrencia[0].Consulta!.Id;

        await using (var db = Db())
        {
            check(await db.Consultas.CountAsync(c => c.MedicoId == 1 && c.Data == day &&
                      c.Horario == horario && c.Status != ConsultaStatus.Cancelada) == 1 &&
                  await db.Ofertas.AnyAsync(o => o.Id == idOferta && o.Estado == "Aceita" &&
                      o.ConsultaId == consultaNova) &&
                  await db.ListasEspera.AnyAsync(l => l.PacienteId == 2 && !l.Ativa),
                "ENCAIXE-PG: uma única consulta ativa, oferta consumida e lista atualizada atomicamente");

            var repetido = await Agenda(db).AgendarAsync(2, 1, day, horario,
                "Repetição indevida", ofertaId: idOferta);
            check(!repetido.Sucesso,
                "ENCAIXE-PG: oferta já aceita não pode ser utilizada novamente");

            // Prova separada de expiração e passagem ao próximo candidato.
            const string segundoHorario = "13:00";
            db.Disponibilidades.Add(new Disponibilidade
            {
                MedicoId = 1, Data = day, Horario = segundoHorario, Ativo = true
            });
            await db.SaveChangesAsync();
            var motor = Motor(db);
            check(await motor.ReservarAsync() == 1,
                "ENCAIXE-PG: nova vaga tem oferta temporária reservada");
            var expirada = await db.Ofertas.Include(o => o.ListaEspera)
                .SingleAsync(o => o.Data == day && o.Horario == segundoHorario && o.Estado == "Aberta");
            check(expirada.ListaEspera.PacienteId == 3,
                "ENCAIXE-PG: prioridade seleciona o primeiro elegível remanescente");
            expirada.ExpiraEm = DateTime.UtcNow.AddSeconds(-10); // relógio sintético, sem esperar 15 minutos
            await db.SaveChangesAsync();
            var aceiteVencido = await Agenda(db).AgendarAsync(3, 1, day, segundoHorario,
                "Aceite fora do prazo", ofertaId: expirada.Id);
            check(!aceiteVencido.Sucesso &&
                  !await db.Consultas.AnyAsync(c => c.MedicoId == 1 && c.Data == day &&
                      c.Horario == segundoHorario && c.Status != ConsultaStatus.Cancelada),
                "ENCAIXE-PG: oferta vencida é recusada sem reservar a consulta");
            var idVencido = expirada.Id;
            db.ChangeTracker.Clear();
            check(await Motor(db).ReservarAsync() == 1 &&
                  await db.Ofertas.AnyAsync(o => o.Id == idVencido && o.Estado == "Expirada") &&
                  await db.Ofertas.Include(o => o.ListaEspera).AnyAsync(o =>
                      o.Data == day && o.Horario == segundoHorario && o.Estado == "Aberta" &&
                      o.ListaEspera.PacienteId == 4),
                "ENCAIXE-PG: expiração libera oferta ao próximo candidato, sem reaproveitar aceite vencido");
        }

        // Dois profissionais da mesma especialidade disputam o mesmo pedido.
        var diaConcorrente = day.AddDays(1);
        await using (var db = Db())
        {
            foreach (var item in await db.ListasEspera.ToListAsync()) item.Ativa = false;
            db.Medicos.Add(new Medico { Id = 2, Nome = "Segundo profissional fictício", EspecialidadeId = 1 });
            db.ListasEspera.Add(new ListaEspera { PacienteId = 1, EspecialidadeId = 1, DataPreferida = diaConcorrente });
            db.Disponibilidades.AddRange(
                new Disponibilidade { MedicoId = 1, Data = diaConcorrente, Horario = "10:00", Ativo = true },
                new Disponibilidade { MedicoId = 2, Data = diaConcorrente, Horario = "10:00", Ativo = true });
            await db.SaveChangesAsync();
        }
        var inicioMotores = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<int> Ofertar(int medico)
        {
            await inicioMotores.Task;
            await using var db = Db();
            return await Motor(db).ReservarAsync(default, medico, diaConcorrente, "10:00");
        }
        var motorA = Ofertar(1);
        var motorB = Ofertar(2);
        inicioMotores.SetResult(true);
        var criadas = await Task.WhenAll(motorA, motorB).WaitAsync(TimeSpan.FromSeconds(30));
        await using (var db = Db())
        {
            check(criadas.Sum() == 1 && await db.Ofertas.CountAsync(o => o.Data == diaConcorrente && o.Estado == "Aberta") == 1,
                "ENCAIXE-PG: profissionais concorrentes não duplicam oferta para o mesmo pedido");
            // Simula a oferta carregada pelo controller antes de outro processo recusá-la.
            var stale = await db.Ofertas.Include(o => o.ListaEspera).SingleAsync(o => o.Data == diaConcorrente && o.Estado == "Aberta");
            await using (var outro = Db())
            {
                var atual = await outro.Ofertas.SingleAsync(o => o.Id == stale.Id);
                atual.Estado = "Recusada";
                await outro.SaveChangesAsync();
            }
            var resultadoStale = await Agenda(db).AgendarAsync(1, stale.MedicoId, stale.Data, stale.Horario,
                "Aceite com entidade antiga", ofertaId: stale.Id);
            check(!resultadoStale.Sucesso && !await db.Consultas.AnyAsync(c => c.Data == diaConcorrente),
                "ENCAIXE-PG: aceite relê oferta já alterada por outra conexão");
        }

        Console.WriteLine("ENCAIXE-PG: execução completa; somente dados sintéticos em banco local isolado.");
    }
}
