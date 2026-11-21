using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;

internal static class AgendamentoChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var clock = new TestClock();

        var (db, service, paciente, medico) = await CriarCenarioAsync(clock, statusRegra: null, convenioValido: true);
        await using (db)
        {
            var resultado = await service.AgendarAsync(paciente.Id, medico.Id, clock.Hoje.AddDays(1), "10:00", null);
            check(resultado.Sucesso && resultado.Consulta?.Status == ConsultaStatus.Confirmada && resultado.Consulta.ConfirmadaEm.HasValue,
                "Agendamento direto confirma a consulta imediatamente sem triagem" );
            check(resultado.Consulta?.TipoPagamento == TipoPagamentoConsulta.Convenio && resultado.Consulta.ConvenioUsado == "Unimed",
                "Convênio cadastrado e válido é aproveitado no agendamento direto sem matriz" );
            check(resultado.Consulta?.StatusElegibilidadeConvenio == ElegibilidadeConvenioStatus.PendenteValidacao,
                "Sem regra para convênio e especialidade, a consulta confirma mas a cobertura fica pendente de validação" );
        }

        var (dbCoberta, serviceCoberta, pacienteCoberta, medicoCoberta) = await CriarCenarioAsync(clock, StatusCoberturaConvenio.Coberta, convenioValido: true);
        await using (dbCoberta)
        {
            var resultado = await serviceCoberta.AgendarAsync(pacienteCoberta.Id, medicoCoberta.Id, clock.Hoje.AddDays(1), "10:00", null,
                TipoPagamentoConsulta.Convenio, permitirEscolhaPagamento: true);
            check(resultado.Sucesso && resultado.Consulta?.StatusElegibilidadeConvenio == ElegibilidadeConvenioStatus.CoberturaNaMatriz,
                "Regra coberta grava a indicação da matriz sem afirmar validação em tempo real da operadora" );
        }

        var (dbAuth, serviceAuth, pacienteAuth, medicoAuth) = await CriarCenarioAsync(clock, StatusCoberturaConvenio.Coberta, convenioValido: true, requerAutorizacao: true);
        await using (dbAuth)
        {
            var resultado = await serviceAuth.AgendarAsync(pacienteAuth.Id, medicoAuth.Id, clock.Hoje.AddDays(1), "10:00", null,
                TipoPagamentoConsulta.Convenio, permitirEscolhaPagamento: true);
            check(resultado.Sucesso && resultado.Consulta?.StatusElegibilidadeConvenio == ElegibilidadeConvenioStatus.AguardandoAutorizacao,
                "Consulta pode ser reservada imediatamente, mas a autorização prévia fica pendente e rastreável" );
        }

        var (dbManual, serviceManual, pacienteManual, medicoManual) = await CriarCenarioAsync(clock, StatusCoberturaConvenio.VerificacaoManual, convenioValido: true);
        await using (dbManual)
        {
            var resultado = await serviceManual.AgendarAsync(pacienteManual.Id, medicoManual.Id, clock.Hoje.AddDays(1), "10:00", null,
                TipoPagamentoConsulta.Convenio, permitirEscolhaPagamento: true);
            check(resultado.Sucesso && resultado.Consulta?.StatusElegibilidadeConvenio == ElegibilidadeConvenioStatus.PendenteValidacao,
                "Regra de verificação manual não bloqueia a vaga, mas cria pendência de validação" );
        }

        var (db2, service2, paciente2, medico2) = await CriarCenarioAsync(clock, StatusCoberturaConvenio.NaoCoberta, convenioValido: true);
        await using (db2)
        {
            var peloConvenio = await service2.AgendarAsync(paciente2.Id, medico2.Id, clock.Hoje.AddDays(1), "10:00", null,
                TipoPagamentoConsulta.Convenio, permitirEscolhaPagamento: true);
            check(!peloConvenio.Sucesso && !await db2.Consultas.AnyAsync(),
                "Regra explícita de não cobertura bloqueia somente a forma de pagamento por convênio" );

            var particular = await service2.AgendarAsync(paciente2.Id, medico2.Id, clock.Hoje.AddDays(1), "10:00", null,
                TipoPagamentoConsulta.Particular, permitirEscolhaPagamento: true);
            check(particular.Sucesso && particular.Consulta?.Status == ConsultaStatus.Confirmada &&
                  particular.Consulta.TipoPagamento == TipoPagamentoConsulta.Particular && particular.Consulta.ConvenioUsado is null &&
                  particular.Consulta.StatusElegibilidadeConvenio == ElegibilidadeConvenioStatus.NaoAplicavel,
                "Paciente pode manter o agendamento como particular quando a matriz informa não cobertura" );
        }

        var (db3, service3, paciente3, medico3) = await CriarCenarioAsync(clock, statusRegra: null, convenioValido: false);
        await using (db3)
        {
            var resultado = await service3.AgendarAsync(paciente3.Id, medico3.Id, clock.Hoje.AddDays(1), "10:00", null,
                TipoPagamentoConsulta.Convenio, permitirEscolhaPagamento: true);
            check(!resultado.Sucesso && !await db3.Consultas.AnyAsync(),
                "Convênio vencido não é aceito como pagamento no agendamento direto" );
        }
    }

    private static async Task<(MKSANContext Db, AgendamentoService Service, Paciente Paciente, Medico Medico)> CriarCenarioAsync(
        TestClock clock, string? statusRegra, bool convenioValido, bool requerAutorizacao = false)
    {
        var options = new DbContextOptionsBuilder<MKSANContext>()
            .UseInMemoryDatabase("agendamento-matriz-" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MKSANContext(options);
        var paciente = new Paciente
        {
            Id = 1, Nome = "Paciente teste", Cpf = "52998224725", Ativo = true,
            TemConvenio = true, NomeConvenio = "Unimed", NumeroConvenio = "TESTE-001",
            ValidadeConvenio = convenioValido ? clock.Hoje.AddDays(60) : clock.Hoje.AddDays(-1)
        };
        var especialidade = new Especialidade { Id = 1, Nome = "Cardiologia", Ativo = true };
        var medico = new Medico { Id = 1, Nome = "Médico teste", Especialidade = "Cardiologia", EspecialidadeId = 1, Ativo = true };
        db.AddRange(paciente, especialidade, medico,
            new Disponibilidade { Id = 1, MedicoId = medico.Id, Data = clock.Hoje.AddDays(1), Horario = "10:00", Ativo = true });
        if (statusRegra is not null)
        {
            db.ConveniosEspecialidades.Add(new ConvenioEspecialidade
            {
                Id = 1, ConvenioNome = "Unimed", ConvenioChave = ConvenioElegibilidadeService.NormalizarChave("Unimed"),
                EspecialidadeId = especialidade.Id, StatusCobertura = statusRegra,
                Coberta = statusRegra != StatusCoberturaConvenio.NaoCoberta,
                RequerAutorizacao = requerAutorizacao, Ativo = true
            });
        }
        await db.SaveChangesAsync();
        var convenio = new ConvenioService(clock);
        var elegibilidade = new ConvenioElegibilidadeService(db, convenio);
        var service = new AgendamentoService(db, clock, new EspecialidadeService(db), convenio, elegibilidade,
            NullLogger<AgendamentoService>.Instance);
        return (db, service, paciente, medico);
    }

    private sealed class TestClock : IClinicaClock
    {
        public DateTime Agora => new(2026, 10, 28, 8, 0, 0, DateTimeKind.Utc);
        public DateTime Hoje => Agora.Date;
        public DateTime ConverterUtc(DateTime utc) => utc;
        public DateTime ConverterParaUtc(DateTime local) => local;
    }
}
