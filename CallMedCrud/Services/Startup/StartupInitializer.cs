using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Database;

namespace MKSANCrud.Services.Startup;

public static class StartupInitializer
{
    private static readonly string[] Roles =
        ["Paciente", "Funcionario", "Admin", "Medico"];

    public static async Task InitializeAsync(
        WebApplication app,
        IConfiguration configuration)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<Usuario>>();
        var context = services.GetRequiredService<MKSANContext>();
        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

        if (configuration.GetValue<bool>("Database:AutoMigrate"))
            await context.Database.MigrateAsync();

        await AplicarPatchesAsync(services, configuration);

        var especialidades = services.GetRequiredService<EspecialidadeService>();
        await especialidades.SincronizarCatalogoAsync();

        await GarantirRolesAsync(roleManager);
        await GarantirBootstrapAdminAsync(
            configuration,
            userManager,
            context,
            logger);
    }

    private static async Task AplicarPatchesAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        if (configuration.GetValue("Database:ApplyV12Patch", true))
            await services.GetRequiredService<DatabaseSchemaV12Initializer>().AplicarAsync();

        if (configuration.GetValue("Database:ApplyV13Patch", true))
            await services.GetRequiredService<DatabaseSchemaV13Initializer>().AplicarAsync();

        if (configuration.GetValue("Database:ApplyV14Patch", true))
            await services.GetRequiredService<DatabaseSchemaV14Initializer>().AplicarAsync();

        if (configuration.GetValue("Database:ApplyV15Patch", true))
            await services.GetRequiredService<DatabaseSchemaV15Initializer>().AplicarAsync();

        if (configuration.GetValue("Database:ApplyV16Patch", true))
            await services.GetRequiredService<DatabaseSchemaV16Initializer>().AplicarAsync();

        if (configuration.GetValue("Database:ApplyV21Patch", true))
            await services.GetRequiredService<DatabaseSchemaV21Initializer>().AplicarAsync();
    }

    private static async Task GarantirRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var resultado = await roleManager.CreateAsync(new IdentityRole(role));
            if (resultado.Succeeded)
                continue;

            var erros = string.Join(", ", resultado.Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                $"Não foi possível criar a role {role}: {erros}");
        }
    }

    private static async Task GarantirBootstrapAdminAsync(
        IConfiguration configuration,
        UserManager<Usuario> userManager,
        MKSANContext context,
        ILogger logger)
    {
        if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
            return;

        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var senha = configuration["BootstrapAdmin:Password"];
        var nome = configuration["BootstrapAdmin:Name"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(senha) ||
            senha.Length < 8)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin habilitado, mas Email/Password não foram configurados corretamente.");
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new Usuario
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var criado = await userManager.CreateAsync(admin, senha);
            if (!criado.Succeeded)
            {
                var erros = string.Join(", ", criado.Errors.Select(e => e.Description));
                throw new InvalidOperationException(
                    $"Falha ao criar BootstrapAdmin: {erros}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, "Admin");
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Não foi possível atribuir a role Admin ao BootstrapAdmin.");
            }
        }

        var funcionario = await context.Funcionarios
            .FirstOrDefaultAsync(f => f.UsuarioId == admin.Id || f.Email == email);

        if (funcionario is null)
        {
            context.Funcionarios.Add(new Funcionario
            {
                UsuarioId = admin.Id,
                Nome = string.IsNullOrWhiteSpace(nome)
                    ? "Administrador CallMed"
                    : nome,
                Email = email,
                Cargo = "Administrador",
                Ativo = true,
                CriadoEm = DateTime.UtcNow
            });
        }
        else
        {
            funcionario.UsuarioId = admin.Id;
            funcionario.Cargo = "Administrador";
            funcionario.Ativo = true;
        }

        await context.SaveChangesAsync();
        logger.LogInformation(
            "BootstrapAdmin verificado com sucesso para {Email}.",
            email);
    }
}
