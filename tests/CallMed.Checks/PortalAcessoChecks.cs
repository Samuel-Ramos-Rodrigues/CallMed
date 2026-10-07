using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Usuarios;

internal static class PortalAcessoChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddDbContext<MKSANContext>(o => o.UseInMemoryDatabase("portais-" + Guid.NewGuid()));
        services.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<MKSANContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MKSANContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var papel in new[] { "Paciente", "Medico", "Funcionario", "Admin" })
            Exigir(await roles.CreateAsync(new IdentityRole(papel)));

        async Task<Usuario> CriarAsync(string email, string papel)
        {
            var user = new Usuario { Email = email, UserName = email, EmailConfirmed = true };
            Exigir(await users.CreateAsync(user, "Senha123!"));
            Exigir(await users.AddToRoleAsync(user, papel));
            return user;
        }
        var paciente = await CriarAsync("paciente@example.test", "Paciente");
        var medico = await CriarAsync("medico@example.test", "Medico");
        var atendente = await CriarAsync("atendente@example.test", "Funcionario");
        var admin = await CriarAsync("admin@example.test", "Admin");
        var semVinculo = await CriarAsync("sem-vinculo@example.test", "Medico");
        var outroMedico = await CriarAsync("outro-medico@example.test", "Medico");
        var legado = await CriarAsync("legado@example.test", "Medico");
        db.Especialidades.Add(new Especialidade { Id = 1, Nome = "Clínica geral" });
        db.Pacientes.Add(new Paciente { Nome = "Paciente", Cpf = "52998224725", Email = paciente.Email, UsuarioId = paciente.Id, Ativo = true });
        var cadastroMedico = new Medico { Nome = "Médico", Crm = "TESTE-1", EspecialidadeId = 1, Email = medico.Email, UsuarioId = medico.Id, Ativo = true };
        var cadastroAtendente = new Funcionario { Nome = "Atendente", Email = atendente.Email!, UsuarioId = atendente.Id, Cargo = "Atendente", Ativo = true };
        db.Medicos.AddRange(cadastroMedico,
            new Medico { Nome = "Outro médico", Crm = "TESTE-2", EspecialidadeId = 1, Email = semVinculo.Email, UsuarioId = outroMedico.Id, Ativo = true },
            new Medico { Nome = "Médico legado", Crm = "TESTE-3", EspecialidadeId = 1, Email = legado.Email, Ativo = true });
        db.Funcionarios.Add(cadastroAtendente);
        await db.SaveChangesAsync();
        var acessos = new PortalAcessoService(db, users);

        // A tela escolhida não pode transformar uma conta em outro perfil.
        foreach (var (user, papel) in new[] { (paciente, "Paciente"), (medico, "Medico"), (atendente, "Funcionario"), (admin, "Admin") })
        {
            foreach (var portal in PortaisAcesso.Todos)
            {
                var conta = await acessos.ObterContaAsync(portal, user.Email!);
                check((conta?.Id == user.Id) == (portal.Papel == papel),
                    $"Conta {papel} no portal {portal.Nome}: {(portal.Papel == papel ? "permitida" : "bloqueada")}");
            }
        }
        check((await acessos.ObterContaAsync(PortaisAcesso.Paciente, "529.982.247-25"))?.Id == paciente.Id,
            "CPF formatado continua permitindo o acesso do paciente vinculado");
        check(await acessos.ObterContaAsync(PortaisAcesso.Medico, "529.982.247-25") is null,
            "Portal profissional não usa CPF de paciente para autenticar");
        check((await acessos.ObterContaAsync(PortaisAcesso.Atendente, "  ATENDENTE@EXAMPLE.TEST  "))?.Id == atendente.Id,
            "E-mail aceita espaços externos e diferença entre maiúsculas e minúsculas");

        cadastroMedico.Ativo = false;
        cadastroAtendente.Ativo = false;
        await db.SaveChangesAsync();
        check(await acessos.ObterContaAsync(PortaisAcesso.Medico, medico.Email!) is null,
            "Médico inativo não recebe uma conta candidata ao login");
        check(await acessos.ObterContaAsync(PortaisAcesso.Atendente, atendente.Email!) is null,
            "Atendente inativo não recebe uma conta candidata ao login");
        check(await acessos.ObterContaAsync(PortaisAcesso.Medico, semVinculo.Email!) is null,
            "E-mail não permite assumir cadastro médico já vinculado a outra conta");
        var vinculos = new UsuarioVinculoService(db, users);
        check(await vinculos.ObterMedicoAsync(Principal(semVinculo, "Medico")) is null,
            "Resolução da sessão também recusa o cadastro de outro médico");
        check((await acessos.ObterContaAsync(PortaisAcesso.Medico, legado.Email!))?.Id == legado.Id,
            "Cadastro médico legado ativo sem vínculo permanece acessível pela própria conta");
        check((await vinculos.ObterMedicoAsync(Principal(legado, "Medico")))?.UsuarioId == legado.Id,
            "Primeiro acesso vincula o cadastro legado à conta correta");

        db.Funcionarios.Add(new Funcionario { Nome = "Admin inativo", Email = admin.Email!, UsuarioId = admin.Id, Cargo = "Administrador", Ativo = false });
        await db.SaveChangesAsync();
        check(await acessos.ObterContaAsync(PortaisAcesso.Administrador, admin.Email!) is null,
            "Administrador com cadastro inativo também é bloqueado");

        var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        foreach (var controller in new[] { typeof(AcessosController), typeof(MedicoAcessoController), typeof(FuncionarioController) })
        {
            var policy = await AuthorizationPolicy.CombineAsync(policies, controller.GetCustomAttributes<AuthorizeAttribute>());
            foreach (var (user, papel) in new[] { (paciente, "Paciente"), (medico, "Medico"), (atendente, "Funcionario"), (admin, "Admin") })
                check((await authorization.AuthorizeAsync(Principal(user, papel), null, policy!)).Succeeded == (papel == "Admin"),
                    $"Permissão de {papel} para {controller.Name}: somente administrador gerencia contas");
            check(!(await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy!)).Succeeded,
                "Visitante sem login não gerencia contas: " + controller.Name);
        }
    }

    private static ClaimsPrincipal Principal(Usuario user, string papel) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Email!), new Claim(ClaimTypes.Role, papel) }, "teste"));

    private static void Exigir(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
