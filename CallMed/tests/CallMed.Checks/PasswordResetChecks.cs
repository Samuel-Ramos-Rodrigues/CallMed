using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Areas.Identity.Pages.Account;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Email;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

internal static class PasswordResetChecks
{
    private const string Anterior = "Antes#2026Senha";
    private const string Nova = "Depois#2026Senha";
    public static async Task RunAsync(Action<bool, string> check)
    {
        var root = new InMemoryDatabaseRoot(); var name = "reset-" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(name, root).Options;
        string id, token, email;
        // Reproduz o comportamento anterior: o hash muda, mas o bloqueio continua.
        using (var provider = Provider(options))
        {
            using var scope = provider.CreateScope(); var services = scope.ServiceProvider;
            var users = services.GetRequiredService<UserManager<Usuario>>(); var db = services.GetRequiredService<MKSANContext>();
            var user = await CriarAsync(services, "paciente@example.test", "Paciente"); id = user.Id; email = user.Email!;
            for (var i = 0; i < 5; i++) Exigir(await users.AccessFailedAsync(user));
            var codigoAnterior = await users.GeneratePasswordResetTokenAsync(user);
            Exigir(await users.ResetPasswordAsync(user, codigoAnterior, "Intermediaria#2026Senha"));
            var aindaBloqueado = await services.GetRequiredService<SignInManager<Usuario>>().CheckPasswordSignInAsync(user, "Intermediaria#2026Senha", false);
            check(aindaBloqueado.IsLockedOut, "Reproduz troca de senha sem remoção do bloqueio temporário de login");
            token = await users.GeneratePasswordResetTokenAsync(user);
            check(await db.DataProtectionKeys.AnyAsync(), "Chaves de recuperação ficam persistidas no mesmo banco da aplicação");
        }
        // Outro contêiner/provedor deve validar o link emitido no anterior.
        using (var provider = Provider(options))
        {
            using var scope = provider.CreateScope(); var services = scope.ServiceProvider;
            var users = services.GetRequiredService<UserManager<Usuario>>(); var db = services.GetRequiredService<MKSANContext>();
            var page = Page(services); var codigo = Codificar(token);
            await page.OnGetAsync(email, codigo, id);
            check(page.MostrarRedefinicao && !page.LinkInvalido, "Link continua válido depois de recriar o provedor da aplicação");
            page.Reset.NovaSenha = Nova; page.Reset.ConfirmarSenha = Nova;
            page.ModelState.AddModelError("Input.Usuario", "Campo do outro formulário");
            page.ModelState.AddModelError("Usuario", "Validação sem prefixo do formulário de solicitação");
            var result = await page.OnPostRedefinirAsync();
            check(result is RedirectToPageResult { PageName: "./ResetPasswordConfirmation" }, "Sucesso segue para confirmação explícita depois de gravar a nova senha");
            db.ChangeTracker.Clear(); var user = (await users.FindByIdAsync(id))!;
            check(await users.CheckPasswordAsync(user, Nova) && !await users.CheckPasswordAsync(user, "Intermediaria#2026Senha"), "Nova senha funciona após recarregar o banco e a senha anterior é recusada");
            check(!await users.IsLockedOutAsync(user) && await users.GetAccessFailedCountAsync(user) == 0 &&
                (await services.GetRequiredService<SignInManager<Usuario>>().CheckPasswordSignInAsync(user, Nova, true)).Succeeded,
                "Recuperação válida remove bloqueio temporário e permite autenticação com a nova senha");
            var acesso = await services.GetRequiredService<PortalAcessoService>().ObterContaUnificadaAsync("529.982.247-25");
            check(acesso.Usuario?.Id == id, "Login por CPF resolve a mesma conta cuja senha foi redefinida");
            var replay = Page(services); await replay.OnGetAsync(email, codigo, id);
            check(replay.LinkInvalido && !replay.MostrarRedefinicao, "Link já usado deixa de oferecer o formulário de nova senha");
            replay.Reset.NovaSenha = "Outra#2026Senha"; replay.Reset.ConfirmarSenha = replay.Reset.NovaSenha;
            check(await replay.OnPostRedefinirAsync() is PageResult && replay.LinkInvalido && await users.CheckPasswordAsync(user, Nova),
                "Repetir o POST com token consumido não altera a senha novamente");

            var outro = await CriarAsync(services, "outro@example.test", "Funcionario");
            var fresh = await users.GeneratePasswordResetTokenAsync(user);
            var adulterado = Page(services); await adulterado.OnGetAsync(outro.Email, Codificar(fresh), id);
            check(adulterado.LinkInvalido && string.IsNullOrEmpty(adulterado.EmailContaMascarado), "UID/e-mail adulterados são recusados antes de exibir a conta");
            adulterado = Page(services); adulterado.Reset = Reset(outro, Codificar(fresh));
            check(await adulterado.OnPostRedefinirAsync() is PageResult && adulterado.LinkInvalido && await users.CheckPasswordAsync(outro, Anterior),
                "Token de uma conta não altera a senha de outra conta");
            var invalido = Page(services); await invalido.OnGetAsync(email, "v1_%%%", id);
            check(invalido.LinkInvalido, "Código Base64URL inválido mostra a opção de solicitar novo link");

            var weak = Page(services); weak.Reset = Reset(user, Codificar(fresh), "abc");
            ValidateReset(weak);
            check(await weak.OnPostRedefinirAsync() is PageResult && !weak.ModelState.IsValid && await users.CheckPasswordAsync(user, Nova),
                "Senha fora das regras permanece no formulário sem alterar o hash");
            var mismatch = Page(services); mismatch.Reset = Reset(user, Codificar(fresh)); mismatch.Reset.ConfirmarSenha = "Diferente#2026Senha";
            ValidateReset(mismatch);
            check(await mismatch.OnPostRedefinirAsync() is PageResult && !mismatch.ModelState.IsValid && await users.CheckPasswordAsync(user, Nova),
                "Confirmação diferente da nova senha é recusada sem gravação");

            var lifespan = services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
            lifespan.TokenLifespan = TimeSpan.FromTicks(-1);
            var expired = Page(services); await expired.OnGetAsync(email, Codificar(fresh), id);
            check(expired.LinkInvalido, "Token expirado é identificado antes de pedir uma nova senha");
            lifespan.TokenLifespan = TimeSpan.FromHours(1);

            foreach (var papel in new[] { "Medico", "Funcionario", "Admin" })
            {
                var perfil = await CriarAsync(services, papel.ToLowerInvariant() + "@example.test", papel);
                var reset = Page(services); reset.Reset = Reset(perfil, await users.GeneratePasswordResetTokenAsync(perfil)); // link legado sem prefixo
                reset.Reset.UsuarioId = "";
                check(await reset.OnPostRedefinirAsync() is RedirectToPageResult && await users.CheckPasswordAsync(perfil, Nova),
                    $"{papel}: recuperação aceita link legado por e-mail e grava a senha na mesma conta");
            }
            var desativado = await CriarAsync(services, "inativo@example.test", "Paciente", false);
            Exigir(await users.SetLockoutEndDateAsync(desativado, DateTimeOffset.MaxValue));
            var bloqueado = Page(services); bloqueado.Reset = Reset(desativado, Codificar(await users.GeneratePasswordResetTokenAsync(desativado)));
            await bloqueado.OnPostRedefinirAsync();
            check((await users.GetLockoutEndDateAsync(desativado))?.Year == 9999 &&
                (await services.GetRequiredService<PortalAcessoService>().ObterContaUnificadaAsync(desativado.Email!)).Usuario is null,
                "Recuperação não remove bloqueio administrativo nem reativa cadastro desativado");
        }
        await CheckPersistenciaAsync(check);
        await CheckHttpAsync(check);
    }

    private static async Task CheckPersistenciaAsync(Action<bool, string> check)
    {
        var interceptor = new SuppressSave();
        var options = new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("sem-save-" + Guid.NewGuid()).AddInterceptors(interceptor).Options;
        using var provider = Provider(options); using var scope = provider.CreateScope(); var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<Usuario>>(); var user = await CriarAsync(services, "gravar@example.test", "Paciente");
        var page = Page(services); page.Reset = Reset(user, Codificar(await users.GeneratePasswordResetTokenAsync(user)));
        interceptor.Suppress = true;
        var result = await page.OnPostRedefinirAsync();
        check(result is PageResult && !page.ModelState.IsValid && await users.CheckPasswordAsync(user, Anterior) && page.TempData["SenhaRedefinida"] is null,
            "Falha simulada de persistência não exibe sucesso nem retorna para o login");
    }

    private static ServiceProvider Provider(DbContextOptions<MKSANContext> options)
    {
        var services = new ServiceCollection(); Configure(services, options); return services.BuildServiceProvider();
    }
    private static void Configure(IServiceCollection services, DbContextOptions<MKSANContext> options)
    {
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Error)); services.AddHttpContextAccessor();
        services.AddScoped<MKSANContext>(_ => new MKSANContext(options));
        services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();
        services.AddIdentity<Usuario, IdentityRole>(o => { o.Password.RequiredLength = 8; o.Lockout.MaxFailedAccessAttempts = 5; o.User.RequireUniqueEmail = true; })
            .AddEntityFrameworkStores<MKSANContext>().AddDefaultTokenProviders().AddPasswordValidator<SpecialCharacterPasswordValidator>();
        services.AddScoped<PasswordRecoveryThrottle>();
        services.AddScoped<PortalAcessoService>(); services.AddSingleton<IClinicaClock, ClinicaClock>(); services.AddSingleton<IEmailService, EmailCaptura>();
    }
    private static async Task<Usuario> CriarAsync(IServiceProvider services, string email, string papel, bool ativo = true)
    {
        var users = services.GetRequiredService<UserManager<Usuario>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(papel)) Exigir(await roles.CreateAsync(new(papel)));
        var user = new Usuario { Email = email, UserName = email, EmailConfirmed = true }; Exigir(await users.CreateAsync(user, Anterior)); Exigir(await users.AddToRoleAsync(user, papel));
        var db = services.GetRequiredService<MKSANContext>();
        if (papel == "Paciente") db.Pacientes.Add(new() { UsuarioId = user.Id, Nome = "Teste", Email = email, Cpf = email.StartsWith("paciente") ? "52998224725" : "11144477735", Ativo = ativo });
        else if (papel == "Medico")
        {
            if (!await db.Especialidades.AnyAsync()) db.Especialidades.Add(new() { Id = 1, Nome = "Clínica geral" });
            db.Medicos.Add(new() { UsuarioId = user.Id, Nome = "Teste", Email = email, Crm = "TESTE-1", EspecialidadeId = 1, Especialidade = "Clínica geral", Ativo = ativo });
        }
        else db.Funcionarios.Add(new() { UsuarioId = user.Id, Nome = "Teste", Email = email, Ativo = ativo });
        await db.SaveChangesAsync(); return user;
    }
    private static ForgotPasswordModel Page(IServiceProvider services)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        return new(services.GetRequiredService<UserManager<Usuario>>(), services.GetRequiredService<MKSANContext>(), services.GetRequiredService<IEmailService>(), NullLogger<ForgotPasswordModel>.Instance)
        { PageContext = new PageContext { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
    }
    private static ForgotPasswordModel.ResetModel Reset(Usuario user, string codigo, string nova = Nova) => new() { Email = user.Email!, UsuarioId = user.Id, Codigo = codigo, NovaSenha = nova, ConfirmarSenha = nova };
    private static string Codificar(string token) => "v1_" + WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static void ValidateReset(ForgotPasswordModel page)
    {
        var results = new List<ValidationResult>(); Validator.TryValidateObject(page.Reset, new ValidationContext(page.Reset), results, true);
        foreach (var r in results) foreach (var name in r.MemberNames.DefaultIfEmpty("")) page.ModelState.AddModelError("Reset." + name, r.ErrorMessage!);
    }
    private static void Exigir(IdentityResult result) { if (!result.Succeeded) throw new Exception(string.Join(";", result.Errors.Select(e => e.Code))); }
    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext c) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext c, IDictionary<string, object> values) { }
    }
    private sealed class SuppressSave : SaveChangesInterceptor
    {
        public bool Suppress;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default) =>
            ValueTask.FromResult(Suppress ? InterceptionResult<int>.SuppressWithResult(0) : result);
    }
    private sealed class EmailCaptura : IEmailService
    {
        public bool Configurado => true; public string Html = "";
        public Task<bool> EnviarAsync(string destinatario, string assunto, string corpoHtml, CancellationToken ct = default, string? replyTo = null) { Html = corpoHtml; return Task.FromResult(true); }
    }

    private static async Task CheckHttpAsync(Action<bool, string> check)
    {
        var contentRoot = Path.GetFullPath("src/CallMed.Web");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ForgotPasswordModel).Assembly.FullName, EnvironmentName = "Development", ContentRootPath = contentRoot });
        builder.Logging.ClearProviders();
        var options = new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("http-reset-" + Guid.NewGuid()).Options;
        Configure(builder.Services, options);
        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorPages(o => o.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "acesso/{perfil=paciente}"));
        await using var app = builder.Build();
        Exception? httpError = null;
        var requestDescription = "";
        app.Use(async (context, next) => { try { await next(context); } catch (Exception ex) { httpError = ex; throw; } });
        app.UseStaticFiles(); app.UseRouting();
        app.Use(async (context, next) =>
        {
            var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.RazorPages.CompiledPageActionDescriptor>();
            requestDescription = $"Método={context.Request.Method} QueryHandler={context.Request.Query["handler"]} RouteHandler={context.Request.RouteValues.GetValueOrDefault("handler")} Handlers={string.Join(",", descriptor?.HandlerMethods.Select(m => m.Name) ?? [])}";
            await next(context);
        });
        app.UseAuthentication(); app.UseAuthorization(); app.MapRazorPages();
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
        try
        {
            using (var scope = app.Services.CreateScope()) await CriarAsync(scope.ServiceProvider, "http@example.test", "Paciente");
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            var confirmationDirect = await client.GetAsync("/Identity/Account/ResetPasswordConfirmation");
            check(confirmationDirect.StatusCode == HttpStatusCode.Redirect, "HTTP: confirmação não apresenta sucesso sem uma redefinição concluída");
            var forgotResponse = await client.GetAsync("/Identity/Account/ForgotPassword");
            if (!forgotResponse.IsSuccessStatusCode) throw new InvalidOperationException("A página de recuperação não abriu no teste HTTP.", httpError);
            var forgot = await forgotResponse.Content.ReadAsStringAsync();
            var response = await client.PostAsync("/Identity/Account/ForgotPassword?handler=Gerar", Form(("__RequestVerificationToken", Field(forgot, "__RequestVerificationToken")), ("Input.Usuario", "http@example.test")));
            check(response.StatusCode == HttpStatusCode.OK && app.Services.GetRequiredService<IEmailService>() is EmailCaptura { Html.Length: > 0 }, "HTTP: solicitar recuperação gera link pelo formulário real e serviço de e-mail simulado");
            var mail = (EmailCaptura)app.Services.GetRequiredService<IEmailService>();
            var href = WebUtility.HtmlDecode(Regex.Match(mail.Html, "href=\"([^\"]+)\"").Groups[1].Value);
            var resetHtml = await client.GetStringAsync(new Uri(href).PathAndQuery);
            check(resetHtml.Contains("Reset.NovaSenha") && resetHtml.Contains("Redefinindo a conta"), "HTTP: link do e-mail abre o formulário real com a conta e o código corretos");
            var fields = new List<KeyValuePair<string,string>> { new("Reset.Email", Field(resetHtml, "Reset.Email")), new("Reset.UsuarioId", Field(resetHtml, "Reset.UsuarioId")), new("Reset.Codigo", Field(resetHtml, "Reset.Codigo")), new("Reset.NovaSenha", Nova), new("Reset.ConfirmarSenha", Nova) };
            response = await client.PostAsync("/Identity/Account/ForgotPassword?handler=Redefinir", new FormUrlEncodedContent(fields));
            check(response.StatusCode == HttpStatusCode.BadRequest, "HTTP: redefinição sem antiforgery é recusada");
            fields.Add(new("__RequestVerificationToken", Field(resetHtml, "__RequestVerificationToken")));
            response = await client.PostAsync("/Identity/Account/ForgotPassword?handler=Redefinir", new FormUrlEncodedContent(fields));
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                var body = await response.Content.ReadAsStringAsync();
                var messages = Regex.Matches(body, "<li\\b[^>]*>(.*?)</li>", RegexOptions.Singleline).Select(m => WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", ""))).ToArray();
                throw new InvalidOperationException("POST de redefinição retornou " + (int)response.StatusCode + ". " + requestDescription + ". Erros: " + string.Join("; ", messages), httpError);
            }
            check(response.Headers.Location!.OriginalString.Contains("ResetPasswordConfirmation"), "HTTP: POST válido grava a senha e redireciona para a confirmação");
            var confirm = await client.GetStringAsync(response.Headers.Location);
            check(confirm.Contains("Senha alterada com sucesso") && confirm.Contains("Entrar com a nova senha"), "HTTP: confirmação mostra sucesso e botão explícito para entrar");
            var login = await client.GetStringAsync("/acesso");
            response = await client.PostAsync("/acesso", Form(("__RequestVerificationToken", Field(login, "__RequestVerificationToken")), ("Input.Usuario", "http@example.test"), ("Input.Password", Nova)));
            check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith(".AspNetCore.Identity.Application=")),
                "HTTP: login real aceita a nova senha e emite o cookie de autenticação");
            using var fresh = app.Services.CreateScope(); var users = fresh.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var user = (await users.FindByEmailAsync("http@example.test"))!;
            check(!await users.CheckPasswordAsync(user, Anterior), "HTTP: senha antiga é rejeitada após o fluxo completo");
        }
        finally { await app.StopAsync(); }
    }
    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) => new(fields.Select(x => new KeyValuePair<string,string>(x.Key,x.Value)));
    private static string Field(string html, string name)
    {
        foreach (Match tag in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
            if (tag.Value.Contains("name=\"" + name + "\"")) return WebUtility.HtmlDecode(Regex.Match(tag.Value, "value=\"([^\"]*)\"").Groups[1].Value);
        throw new Exception("Campo ausente: " + name);
    }
}
