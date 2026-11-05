using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MKSANCrud.Areas.Identity.Pages.Account;
using MKSANCrud.Data;
using MKSANCrud.Middleware;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

internal static class ContaPerfilChecks
{
    private const string Original = "Conta#2026Senha";
    private const string Nova = "Nova#2026Senha";
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    public static async Task RunAsync(Action<bool,string> check)
    {
        check(ContaFotoValidador.Tipo(Png) == "image/png" && ContaFotoValidador.Tipo(Encoding.UTF8.GetBytes("<svg onload=alert(1)></svg>")) is null,
            "Foto aceita PNG e recusa SVG/conteúdo ativo, independentemente do nome do arquivo");
        var grande = new byte[ContaFotoValidador.LimiteBytes + 1];
        check(ContaFotoValidador.Tipo(grande) is null && ContaFotoValidador.Tipo(Png[..^3]) is null, "Foto recusa tamanho excedido e imagem truncada");
        var dimensoes = Png.ToArray(); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(dimensoes.AsSpan(16,4),100000);
        check(ContaFotoValidador.Tipo(dimensoes) is null && ContaService.DescreverNavegador("<script>segredo</script>") == "Navegador · outro dispositivo", "Foto limita dimensões e atividade não armazena User-Agent bruto");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(LoginModel).Assembly.FullName, EnvironmentName = "Development", ContentRootPath = Path.GetFullPath("CallMedCrud") });
        builder.Logging.ClearProviders();
        var databaseName = "perfil-http-" + Guid.NewGuid();
        builder.Services.AddDbContext<MKSANContext>(o => o.UseInMemoryDatabase(databaseName));
        builder.Services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();
        builder.Services.AddIdentity<Usuario,IdentityRole>(o => {o.User.RequireUniqueEmail=true; o.Password.RequiredLength=8;})
            .AddEntityFrameworkStores<MKSANContext>().AddDefaultTokenProviders().AddPasswordValidator<SpecialCharacterPasswordValidator>();
        builder.Services.Configure<SecurityStampValidatorOptions>(o => o.OnRefreshingPrincipal = context => {
            var claim = context.CurrentPrincipal?.FindFirst(ContaService.SessaoClaim);
            if (claim is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity) identity.AddClaim(claim);
            return Task.CompletedTask;
        });
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ContaService>(); builder.Services.AddScoped<UsuarioVinculoService>();
        builder.Services.AddScoped<PortalAcessoService>(); builder.Services.AddScoped<ConfirmacoesService>(); builder.Services.AddSingleton<IClinicaClock,ClinicaClock>();
        builder.Services.AddControllersWithViews(); builder.Services.AddRazorPages(o => o.Conventions.AddAreaPageRoute("Identity","/Account/Login","acesso/{perfil=paciente}"));
        await using var app = builder.Build(); Exception? failure=null;
        app.Use(async (c,next) => {try{await next(c);}catch(Exception ex){failure=ex;throw;}});
        app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseMiddleware<ActiveAccountMiddleware>(); app.UseAuthorization();
        app.MapRazorPages(); app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
        try
        {
            var ids = new Dictionary<string,string>();
            using(var scope=app.Services.CreateScope())
            {
                var services=scope.ServiceProvider; var users=services.GetRequiredService<UserManager<Usuario>>(); var roles=services.GetRequiredService<RoleManager<IdentityRole>>(); var db=services.GetRequiredService<MKSANContext>();
                db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});
                foreach(var role in new[]{"Paciente","Medico","Funcionario","Admin"})
                {
                    Must(await roles.CreateAsync(new(role)));
                    var email=role.ToLowerInvariant()+"@perfil.test"; var user=new Usuario {UserName=email,Email=email,EmailConfirmed=true};
                    Must(await users.CreateAsync(user,Original)); Must(await users.AddToRoleAsync(user,role)); ids[role]=user.Id;
                    if(role=="Paciente") db.Pacientes.Add(new(){UsuarioId=user.Id,Nome="Alex Teste",Cpf="52998224725",Email=email,Telefone="5511999990000",Ativo=true,DataNascimento=new DateTime(2000,1,1)});
                    else if(role=="Medico") db.Medicos.Add(new(){UsuarioId=user.Id,Nome="Dra. Teste",Email=email,Crm="CRM-TESTE",EspecialidadeId=1,Especialidade="Clínica geral",Ativo=true});
                    else if(role=="Funcionario") db.Funcionarios.Add(new(){UsuarioId=user.Id,Nome="Recepção Teste",Email=email,Cargo="Recepcionista",Ativo=true});
                }
                await db.SaveChangesAsync();
            }
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            HttpClient Client() => new(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=new Uri(address)};
            async Task<string> Page(HttpClient client,string url="/MinhaConta")
            {
                var response=await client.GetAsync(url); if(!response.IsSuccessStatusCode)throw new Exception("Perfil HTTP "+(int)response.StatusCode+" "+url,failure);
                return await response.Content.ReadAsStringAsync();
            }
            async Task<HttpResponseMessage> Login(HttpClient client,string email,string password=Original)
            {
                var html=await Page(client,"/acesso");
                var response=await client.PostAsync("/acesso",Form(("__RequestVerificationToken",Field(html,"__RequestVerificationToken")),("Input.Usuario",email),("Input.Password",password)));
                if(response.StatusCode!=HttpStatusCode.Redirect)throw new Exception("Login do teste recusado: "+email+" "+await response.Content.ReadAsStringAsync(),failure);
                return response;
            }
            async Task<HttpResponseMessage> Post(HttpClient client,string action,params (string,string)[] values)
            {
                var html=await Page(client); return await client.PostAsync("/MinhaConta/"+action,Form(values.Append(("__RequestVerificationToken",Field(html,"__RequestVerificationToken"))).ToArray()));
            }
            using var patient=Client(); using var otherDevice=Client(); using var medico=Client();
            check((await patient.GetAsync("/MinhaConta")).StatusCode==HttpStatusCode.Redirect,"Minha Conta exige autenticação");
            await Login(patient,"paciente@perfil.test"); await Login(otherDevice,"paciente@perfil.test"); await Login(medico,"medico@perfil.test");
            var html=await Page(patient);
            check(html.Contains("data-account-tab=\"dados\"") && html.Contains("data-account-tab=\"seguranca\"") && html.Contains("data-account-tab=\"preferencias\"") && html.Contains("Este acesso"),"Perfil real renderiza três abas e identifica o acesso atual");
            var fixtures=Environment.GetEnvironmentVariable("CALLMED_CONTA_FIXTURES");
            async Task Snapshot(string name,string body){if(fixtures is not null){Directory.CreateDirectory(fixtures); await File.WriteAllTextAsync(Path.Combine(fixtures,name+".html"),body);}}
            await Snapshot("paciente",html); await Snapshot("medico",await Page(medico));
            check((await patient.PostAsync("/MinhaConta/SalvarDados",Form(("Dados.Nome","Invasor")))).StatusCode==HttpStatusCode.BadRequest,"Alterações do perfil sem antiforgery são recusadas");
            (string,string)[] Data(string email="paciente@perfil.test",string password="")=>[("Dados.Nome","Alex Atualizado"),("Dados.Email",email),("Dados.Telefone","5579999990000"),("Dados.DataNascimento","2000-01-01"),("Dados.SenhaAtual",password),("UsuarioId",ids["Medico"]),("Dados.Cpf","00000000000"),("Dados.Crm","INVADIDO"),("Dados.Papel","Admin")];
            var result=await Post(patient,"SalvarDados",Data());
            check(result.StatusCode==HttpStatusCode.Redirect,"Salvar dados próprios retorna confirmação após persistir");
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>(); var p=await db.Pacientes.SingleAsync(); var m=await db.Medicos.SingleAsync();
                check(p.Nome=="Alex Atualizado" && p.Telefone=="5579999990000" && p.Cpf=="52998224725" && m.Nome=="Dra. Teste" && m.Crm=="CRM-TESTE", "Binding protege CPF, CRM, papel e cadastro de outro usuário");
            }
            using(var semPrefs=Client())
            {
                var loginSemPrefs=await Login(semPrefs,"paciente@perfil.test");
                check(!loginSemPrefs.Headers.GetValues("Set-Cookie").Any(c=>c.StartsWith(ContaService.CookiePreferencias+"=")),"Salvar apenas dados não substitui aparência existente do navegador no próximo login");
            }
            result=await Post(patient,"SalvarDados",Data("alterado@perfil.test"));
            check(result.StatusCode==HttpStatusCode.OK && (await result.Content.ReadAsStringAsync()).Contains("Confirme sua senha atual"),"Alterar e-mail exige senha atual mesmo com cookie válido");
            result=await Post(patient,"SalvarDados",Data("medico@perfil.test",Original));
            check(result.StatusCode==HttpStatusCode.OK && (await result.Content.ReadAsStringAsync()).Contains("associado a outro cadastro"),"E-mail de outra conta não pode ser usado");
            result=await Post(patient,"SalvarPreferencias",("Preferencias.Tema","dark"),("Preferencias.TextoMaior","true"),("Preferencias.AvisosWhatsApp","false"),("Preferencias.AvisosEmail","true"),("Preferencias.AvisosSms","false"),("Preferencias.CanalPreferido","Email"));
            check(result.StatusCode==HttpStatusCode.Redirect && result.Headers.GetValues("Set-Cookie").Any(c=>c.StartsWith(ContaService.CookiePreferencias+"=")),"Preferências gravadas emitem aparência para aplicar no navegador");
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>(); var pref=await db.ContaPerfis.SingleAsync();
                check(pref.Tema=="dark" && pref.TextoMaior && !pref.AvisosSms && !pref.AvisosWhatsApp && pref.AvisosEmail && (await db.Pacientes.SingleAsync()).CanalPreferido=="Email", "Preferências e canais são persistidos no usuário correto");
            }
            result=await Post(patient,"SalvarPreferencias",("Preferencias.Tema","<script>"));
            check(result.StatusCode==HttpStatusCode.OK && (await result.Content.ReadAsStringAsync()).Contains("data-aba=\"preferencias\""),"Preferências inválidas mantêm a aba correta sem gravar tema arbitrário");
            async Task<HttpResponseMessage> Photo(byte[] bytes,string type)
            {
                using var content=new MultipartFormDataContent(); content.Add(new StringContent(Field(await Page(patient),"__RequestVerificationToken")),"__RequestVerificationToken");
                var file=new ByteArrayContent(bytes); file.Headers.ContentType=new(type); content.Add(file,"foto","../../avatar.png"); return await patient.PostAsync("/MinhaConta/Foto",content);
            }
            check((await Photo(Png,"image/png")).StatusCode==HttpStatusCode.Redirect && (await patient.GetByteArrayAsync("/MinhaConta/Foto")).SequenceEqual(Png),"Foto real é salva e servida apenas para o titular autenticado");
            check((await medico.GetAsync("/MinhaConta/Foto?usuarioId="+ids["Paciente"])).StatusCode==HttpStatusCode.NotFound,"Parâmetro de usuário não permite acessar foto alheia");
            check((await Photo(Encoding.UTF8.GetBytes("<svg onload=alert(1) />"),"image/png")).StatusCode==HttpStatusCode.OK && (await patient.GetByteArrayAsync("/MinhaConta/Foto")).SequenceEqual(Png),"Tipo de upload falsificado é recusado e foto anterior preservada");
            Guid otherSession;
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>(); otherSession=await db.ContaSessoes.Where(x=>x.UsuarioId==ids["Medico"]).Select(x=>x.Id).SingleAsync();
            }
            check((await Post(patient,"EncerrarSessao",("id",otherSession.ToString()))).StatusCode==HttpStatusCode.NotFound,"Paciente não pode encerrar sessão de outro usuário");
            result=await Post(patient,"AlterarSenha",("Senha.SenhaAtual","errada"),("Senha.NovaSenha",Nova),("Senha.ConfirmarSenha",Nova));
            html=await result.Content.ReadAsStringAsync(); await Snapshot("erro-senha",html);
            check(result.StatusCode==HttpStatusCode.OK && html.Contains("data-aba=\"seguranca\"") && !html.Contains("value=\""+Nova+"\""),"Senha atual incorreta fica na aba Segurança sem expor senhas no HTML");
            result=await Post(patient,"AlterarSenha",("Senha.SenhaAtual",Original),("Senha.NovaSenha",Nova),("Senha.ConfirmarSenha",Nova));
            check(result.StatusCode==HttpStatusCode.Redirect && (await patient.GetAsync("/MinhaConta")).StatusCode==HttpStatusCode.OK,"Troca de senha mantém o acesso atual autenticado");
            check((await otherDevice.GetAsync("/MinhaConta")).Headers.Location?.OriginalString.Contains("sessaoEncerrada=true")==true,"Outro dispositivo perde acesso imediatamente após troca de senha");
            using(var scope=app.Services.CreateScope())
            {
                var users=scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>(); var user=(await users.FindByIdAsync(ids["Paciente"]))!;
                check(await users.CheckPasswordAsync(user,Nova) && !await users.CheckPasswordAsync(user,Original),"Nova senha foi gravada e senha anterior é recusada");
            }
            var loginComPrefs=await Login(otherDevice,"paciente@perfil.test",Nova);
            check(loginComPrefs.Headers.GetValues("Set-Cookie").Any(c=>c.StartsWith(ContaService.CookiePreferencias+"=")),"Preferências salvas são aplicadas ao entrar em outro navegador");
            check((await Post(patient,"EncerrarOutrasSessoes")).StatusCode==HttpStatusCode.Redirect && (await patient.GetAsync("/MinhaConta")).StatusCode==HttpStatusCode.OK && (await otherDevice.GetAsync("/MinhaConta")).StatusCode==HttpStatusCode.Redirect,"Encerrar outros acessos preserva o dispositivo atual e revoga o outro");
            // Renovação periódica não pode apagar o identificador rastreável.
            using(var scope=app.Services.CreateScope()) scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval=TimeSpan.Zero;
            await Task.Delay(30); check((await Page(patient)).Contains("Este acesso"),"Renovação do Identity preserva a identificação do acesso atual");
            result=await Post(patient,"SalvarDados",Data("alterado@perfil.test",Nova));
            check(result.StatusCode==HttpStatusCode.Redirect && (await Page(patient)).Contains("alterado@perfil.test"),"Alterar e-mail com senha atual sincroniza Identity e perfil e mantém login");
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>(); var user=await db.Users.SingleAsync(u=>u.Id==ids["Paciente"]);
                check(user.Email=="alterado@perfil.test" && user.UserName==user.Email && (await db.Pacientes.SingleAsync()).Email==user.Email,"E-mail de login e cadastro clínico permanecem sincronizados");
            }
            foreach(var role in new[]{"Funcionario","Admin"})
            {
                using var client=Client(); await Login(client,role.ToLowerInvariant()+"@perfil.test"); html=await Page(client); await Snapshot(role.ToLowerInvariant(),html);
                check(html.Contains("data-account-page") && !html.Contains("Dados.TemConvenio") && !html.Contains("AvisosWhatsApp"),role+": perfil próprio sem campos exclusivos do paciente");
            }
            html=await Page(medico); check(html.Contains("CRM-TESTE") && !html.Contains("name=\"Dados.Crm\"") && html.Contains("Cl&#xED;nica geral"),"Médico vê CRM e especialidade protegidos");
            result=await Post(patient,"RemoverFoto"); check(result.StatusCode==HttpStatusCode.Redirect && (await patient.GetAsync("/MinhaConta/Foto")).StatusCode==HttpStatusCode.NotFound,"Remover foto apaga o conteúdo do próprio perfil");
            result=await Post(patient,"Delete",("Confirmacao.SenhaAtual",Nova),("Confirmacao.Confirmar","false"));
            check(result.StatusCode==HttpStatusCode.OK && (await patient.GetAsync("/MinhaConta")).StatusCode==HttpStatusCode.OK,"Remoção de acesso exige confirmação explícita");
            using(var scope=app.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>(); var p=await db.Pacientes.SingleAsync(); p.Ativo=false; await db.SaveChangesAsync();
            }
            check((await patient.GetAsync("/MinhaConta")).Headers.Location?.OriginalString.Contains("contaInativa=true")==true,"Conta clínica inativa também perde acesso ao novo perfil");
        }
        finally { await app.StopAsync(); }
    }
    private static void Must(IdentityResult r){if(!r.Succeeded)throw new Exception(string.Join(";",r.Errors.Select(x=>x.Description)));}
    private static FormUrlEncodedContent Form(params (string Key,string Value)[] f)=>new(f.Select(x=>new KeyValuePair<string,string>(x.Key,x.Value)));
    private static string Field(string html,string name)
    {
        foreach(Match m in Regex.Matches(html,"<input\\b[^>]*>")) if(m.Value.Contains("name=\""+name+"\""))return WebUtility.HtmlDecode(Regex.Match(m.Value,"value=\"([^\"]*)\"").Groups[1].Value);
        throw new Exception("Campo ausente: "+name);
    }
}
