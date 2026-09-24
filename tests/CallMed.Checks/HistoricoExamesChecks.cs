using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

internal static class HistoricoExamesChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddDbContext<MKSANContext>(o => o.UseInMemoryDatabase("exames-" + Guid.NewGuid()));
        services.AddIdentityCore<Usuario>().AddEntityFrameworkStores<MKSANContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MKSANContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var clock = new TestClock();
        context.Pacientes.AddRange(
            new Paciente { Id = 11, Nome = "Paciente A", Cpf = "00000000001", UsuarioId = "conta-a", Email = "a@example.test" },
            new Paciente { Id = 12, Nome = "Paciente B", Cpf = "00000000002", UsuarioId = "conta-b", Email = "b@example.test" },
            new Paciente { Id = 13, Nome = "Paciente sem exames", Cpf = "00000000003", UsuarioId = "conta-c", Email = "c@example.test" });
        context.ExamesHistorico.AddRange(
            new ExameHistorico { Id = 101, PacienteId = 11, Nome = "Exame A", DataRealizacao = clock.Hoje, Ativo = true },
            new ExameHistorico { Id = 102, PacienteId = 12, Nome = "Exame B", DataRealizacao = clock.Hoje, Ativo = true },
            new ExameHistorico { Id = 103, PacienteId = 11, Nome = "Exame desativado", DataRealizacao = clock.Hoje, Ativo = false });
        context.Especialidades.Add(new Especialidade { Id = 1, Nome = "Clínica geral" });
        context.Medicos.Add(new Medico { Id = 21, Nome = "Médico de teste", UsuarioId = "conta-medico", EspecialidadeId = 1 });
        context.Consultas.AddRange(
            new Consulta { Id = 31, PacienteId = 11, MedicoId = 21, Data = clock.Hoje, Horario = "09:00", Status = ConsultaStatus.Confirmada },
            new Consulta { Id = 32, PacienteId = 12, MedicoId = 21, Data = clock.Hoje, Horario = "10:00", Status = ConsultaStatus.Cancelada });
        await context.SaveChangesAsync();

        var http = new DefaultHttpContext();
        var controller = new HistoricoExamesController(context, new UsuarioVinculoService(context, userManager),
            new AuditoriaService(context, new HttpContextAccessor { HttpContext = http }, NullLogger<AuditoriaService>.Instance), clock)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var paciente = Principal("conta-a", "Paciente", "a@example.test");
        http.User = paciente;
        var pagina = (await controller.Index(null, default) as ViewResult)?.Model as HistoricoExamesViewModel;
        check(pagina?.Paciente.Id == 11 && pagina.Exames.Select(x => x.Id).SequenceEqual(new[] { 101 }),
            "Meus exames sem ID mostra somente registros ativos do paciente logado");
        check(await controller.Index(12, default) is ForbidResult,
            "Paciente não pode consultar exames de outra pessoa alterando o ID");
        check(await controller.Index(11, default) is ViewResult,
            "Link existente com o próprio ID continua funcionando");
        check(await controller.Index(0, default) is ForbidResult && await controller.Index(-1, default) is ForbidResult,
            "IDs explícitos inválidos não são tratados como atalho pessoal");

        http.User = Principal("conta-b", "Paciente", "b@example.test");
        pagina = (await controller.Index(null, default) as ViewResult)?.Model as HistoricoExamesViewModel;
        check(pagina?.Paciente.Id == 12 && pagina.Exames.Select(x => x.Id).SequenceEqual(new[] { 102 }),
            "Outra conta recebe apenas o próprio histórico");
        http.User = Principal("conta-c", "Paciente", "c@example.test");
        pagina = (await controller.Index(null, default) as ViewResult)?.Model as HistoricoExamesViewModel;
        check(pagina?.Paciente.Id == 13 && pagina.Exames.Count == 0,
            "Paciente sem exames recebe a página vazia, não acesso negado");
        http.User = Principal("sem-vinculo", "Paciente", "sem-vinculo@example.test");
        check(await controller.Index(null, default) is ForbidResult,
            "Conta sem paciente vinculado não recebe histórico alheio");

        foreach (var role in new[] { "Funcionario", "Admin" })
        {
            http.User = Principal("conta-equipe", role, "equipe@example.test");
            check(await controller.Index(12, default) is ViewResult,
                role + " mantém a consulta ao histórico do paciente selecionado");
        }
        http.User = Principal("conta-medico", "Medico", "medico@example.test");
        check(await controller.Index(11, default) is ViewResult && await controller.Index(12, default) is ForbidResult,
            "Acesso médico continua exigindo consulta vinculada não cancelada");

        var policyProvider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        foreach (var action in new[] { nameof(HistoricoExamesController.Index), nameof(HistoricoExamesController.Create), nameof(HistoricoExamesController.Desativar) })
        {
            var attributes = typeof(HistoricoExamesController).GetCustomAttributes<AuthorizeAttribute>()
                .Concat(typeof(HistoricoExamesController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, attributes);
            var permitido = (await authorization.AuthorizeAsync(paciente, null, policy!)).Succeeded;
            check(permitido == (action == nameof(HistoricoExamesController.Index)),
                "Política de autorização do paciente: " + action + (permitido ? " permitido" : " bloqueado"));
        }
    }

    private static ClaimsPrincipal Principal(string id, string role, string email) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, email), new Claim(ClaimTypes.Role, role) }, "teste"));

    private sealed class TestClock : IClinicaClock
    {
        public DateTime Agora => new(2026, 9, 24, 9, 0, 0);
        public DateTime Hoje => Agora.Date;
        public DateTime ConverterUtc(DateTime utc) => utc;
        public DateTime ConverterParaUtc(DateTime local) => local;
    }
}
