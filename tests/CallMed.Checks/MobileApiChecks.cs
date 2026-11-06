using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Agente;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Mobile;
using MKSANCrud.Services.Usuarios;

internal static class MobileApiChecks
{
    const string Password="Paciente#2026";
    public static async Task RunAsync(Action<bool,string> check)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(MobileAuth).Assembly.FullName,EnvironmentName="Development",ContentRootPath=Path.GetFullPath("CallMedCrud")});
        builder.Logging.ClearProviders(); var database="mobile-http-"+Guid.NewGuid();
        builder.Services.AddDbContext<MKSANContext>(o=>o.UseInMemoryDatabase(database).ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();
        builder.Services.AddIdentity<Usuario,IdentityRole>(o=>{o.User.RequireUniqueEmail=true;o.Password.RequiredLength=8;}).AddEntityFrameworkStores<MKSANContext>().AddDefaultTokenProviders();
        builder.Services.AddHttpContextAccessor();builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddCallMedMobile();
        builder.Services.AddScoped<PortalAcessoService>();builder.Services.AddScoped<UsuarioVinculoService>();builder.Services.AddScoped<ContaService>();
        builder.Services.AddSingleton<IClinicaClock,ClinicaClock>();builder.Services.AddScoped<ConvenioService>();builder.Services.AddScoped<ConvenioElegibilidadeService>();builder.Services.AddScoped<EspecialidadeService>();builder.Services.AddScoped<AgendamentoService>();
        builder.Services.AddScoped<AtendimentoIdentidadeService>();builder.Services.AddScoped<AtendimentoConversaService>();builder.Services.AddScoped<AtendimentoEnvioService>();builder.Services.AddScoped<AtendimentoFeedbackService>();builder.Services.AddScoped<AgenteHistoricoService>();
        builder.Services.Configure<GmailInboxOptions>(_=>{});builder.Services.AddSingleton<IAgenteClinicaService,FakeAgent>();
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("agente",x=>{x.PermitLimit=100;x.Window=TimeSpan.FromMinutes(1);}));
        builder.Services.AddControllersWithViews();
        await using var app=builder.Build(); Exception? failure=null;
        app.Use(async(c,next)=>{try{await next(c);}catch(Exception ex){failure=ex;throw;}});
        app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();app.MapControllers();
        // Uma sessão de navegador real verifica a separação entre cookie web e bearer móvel.
        app.MapGet("/fixture/cookie",async(HttpContext c,SignInManager<Usuario> sign,UserManager<Usuario> users)=>{await sign.SignInAsync((await users.FindByEmailAsync("a@mobile.test"))!,false);return Results.Ok();});
        app.Urls.Add("http://127.0.0.1:0");await app.StartAsync();
        try
        {
            var ids=new Dictionary<string,string>();var day=app.Services.GetRequiredService<IClinicaClock>().Hoje.AddDays(10);var date=day.ToString("yyyy-MM-dd");
            using(var scope=app.Services.CreateScope())
            {
                var sp=scope.ServiceProvider;var users=sp.GetRequiredService<UserManager<Usuario>>();var roles=sp.GetRequiredService<RoleManager<IdentityRole>>();var db=sp.GetRequiredService<MKSANContext>();
                foreach(var role in new[]{"Paciente","Medico","Funcionario","Admin"})Must(await roles.CreateAsync(new(role)));
                foreach(var name in new[]{"a","b","inativo","medico","funcionario","admin","misto","doisfatores","bloqueio"})
                {
                    var user=new Usuario{UserName=name+"@mobile.test",Email=name+"@mobile.test",EmailConfirmed=true};Must(await users.CreateAsync(user,Password));ids[name]=user.Id;
                    var role=name switch{"medico"=>"Medico","funcionario"=>"Funcionario","admin"=>"Admin",_=>"Paciente"};Must(await users.AddToRoleAsync(user,role));
                    if(role=="Paciente")db.Pacientes.Add(new(){Id=name=="a"?1:name=="b"?2:name=="inativo"?3:name=="misto"?4:name=="doisfatores"?5:6,UsuarioId=user.Id,Nome="Paciente "+name,Cpf=name=="a"?"52998224725":name=="b"?"11144477735":"12345678901",Email=user.Email,Telefone="5579999990000",Ativo=name!="inativo",DataNascimento=new DateTime(2000,1,1)});
                    if(name=="misto")Must(await users.AddToRoleAsync(user,"Admin"));
                    if(name=="doisfatores")Must(await users.SetTwoFactorEnabledAsync(user,true));
                }
                db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});db.Medicos.Add(new(){Id=1,Nome="Dra. Teste",Email="medico@mobile.test",UsuarioId=ids["medico"],Crm="CRM-TESTE",EspecialidadeId=1,Especialidade="Clínica geral",Ativo=true});
                db.Medicos.Add(new(){Id=2,Nome="Inativo",Email="inativo-medico@test.invalid",Crm="CRM-INATIVO",EspecialidadeId=1,Especialidade="Clínica geral",Ativo=false});
                foreach(var h in new[]{"09:00","10:00","11:00"})db.Disponibilidades.Add(new(){MedicoId=1,Data=day,Horario=h,Ativo=true});
                db.Consultas.Add(new(){Id=100,PacienteId=2,MedicoId=1,Data=day.AddDays(1),Horario="09:00"});
                await db.SaveChangesAsync();
            }
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()}){BaseAddress=new Uri(address)};
            async Task<JsonElement> Json(HttpResponseMessage response){var body=await response.Content.ReadAsStringAsync();if(!response.IsSuccessStatusCode)throw new Exception("Mobile HTTP "+(int)response.StatusCode+" "+body,failure);return JsonDocument.Parse(body).RootElement.Clone();}
            async Task<JsonElement> Login(HttpClient c,string user="a@mobile.test",string password=Password)
            {
                var r=await c.PostAsJsonAsync("/api/mobile/v1/auth/login",new{usuario=user,senha=password});var j=await Json(r);
                check(!r.Headers.Contains("Set-Cookie") && j.GetProperty("tokenType").GetString()=="Bearer","Login nativo emite tokens sem criar cookie do site");
                c.DefaultRequestHeaders.Authorization=new("Bearer",j.GetProperty("accessToken").GetString());return j;
            }
            using var a=Client();using var b=Client();using var device2=Client();using var cookie=Client();
            check((await a.GetAsync("/api/mobile/v1/config")).IsSuccessStatusCode,"Configuração pública identifica API nativa");
            foreach(var path in new[]{"perfil","consultas","atendimento/historico"})check((await a.GetAsync("/api/mobile/v1/"+path)).StatusCode==HttpStatusCode.Unauthorized,"API móvel exige bearer para "+path);
            await cookie.GetAsync("/fixture/cookie");check((await cookie.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized,"Cookie web não autentica API móvel");
            foreach(var name in new[]{"inativo","medico","funcionario","admin","misto","doisfatores"})check((await a.PostAsJsonAsync("/api/mobile/v1/auth/login",new{usuario=name+"@mobile.test",senha=Password})).StatusCode==HttpStatusCode.Unauthorized,"Login nativo recusa acesso "+name);
            var tokens=await Login(a,"529.982.247-25");var tokens2=await Login(device2);await Login(b,"b@mobile.test");
            var profile=await Json(await a.GetAsync("/api/mobile/v1/perfil?usuarioId="+ids["b"]));check(profile.GetProperty("nome").GetString()=="Paciente a","Perfil ignora identificador de outro usuário");
            var update=await a.PutAsJsonAsync("/api/mobile/v1/perfil",new{nome="Alex Atualizado",telefone="5579999990000",dataNascimento="2000-01-01",temConvenio=false,cpf="00000000000",usuarioId=ids["b"],email="troca@test.invalid",papel="Admin"});
            profile=await Json(update);check(profile.GetProperty("cpf").GetString()=="52998224725"&&profile.GetProperty("email").GetString()=="a@mobile.test","Atualização móvel protege CPF, e-mail, papel e vínculo");
            check((await a.PutAsJsonAsync("/api/mobile/v1/perfil",new{nome="Futuro",dataNascimento="2999-01-01"})).StatusCode==HttpStatusCode.BadRequest,"Perfil recusa nascimento futuro");
            check((await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{tema="arbitrario"})).StatusCode==HttpStatusCode.BadRequest,"Preferências recusam tema inválido");
            var pref=await Json(await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{tema="light",avisosWhatsApp=false,avisosEmail=true,avisosSms=false,canalPreferido="Email"}));check(pref.GetProperty("tema").GetString()=="light"&&!pref.GetProperty("avisosSms").GetBoolean(),"Preferências móveis persistem na conta compartilhada");
            using(var photo=new MultipartFormDataContent()){photo.Add(new ByteArrayContent(ContaPerfilChecks.Png),"foto","foto.png");check((await a.PostAsync("/api/mobile/v1/perfil/foto",photo)).IsSuccessStatusCode,"Upload nativo aceita foto válida");}
            check((await a.GetByteArrayAsync("/api/mobile/v1/perfil/foto")).SequenceEqual(ContaPerfilChecks.Png)&&(await b.GetAsync("/api/mobile/v1/perfil/foto?usuarioId="+ids["a"])).StatusCode==HttpStatusCode.NotFound,"Foto móvel é restrita à própria conta");
            var doctors=await Json(await a.GetAsync("/api/mobile/v1/medicos"));check(doctors.GetArrayLength()==1,"Catálogo nativo omite médico inativo");
            check((await a.GetAsync("/api/mobile/v1/medicos/1/datas?consultaId=100")).StatusCode==HttpStatusCode.NotFound,"Disponibilidade não permite ignorar consulta alheia");
            check((await Json(await a.GetAsync("/api/mobile/v1/medicos/1/datas"))).EnumerateArray().Any(x=>x.GetString()==date),"Datas móveis vêm da agenda real");
            object Booking(bool confirm=true,string hour="09:00",string payment="Particular")=>new{medicoId=1,data=date,horario=hour,pagamento=payment,confirmar=confirm,pacienteId=2};
            check((await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking(false))).StatusCode==HttpStatusCode.BadRequest,"Agendamento exige confirmação explícita");
            check(!(await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking(true,"09:00","Convenio"))).IsSuccessStatusCode,"Agendamento não aceita convênio inexistente");
            var booked=await Json(await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking()));var id=booked.GetProperty("consulta").GetProperty("id").GetInt32();
            var again=await Json(await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking()));check(again.GetProperty("consulta").GetProperty("id").GetInt32()==id,"Repetição do mesmo agendamento não duplica consulta");
            check(!(await b.PostAsJsonAsync("/api/mobile/v1/consultas",Booking())).IsSuccessStatusCode,"Outro paciente não ocupa horário já reservado");
            foreach(var action in new[]{"cancelar","confirmar","remarcar"})check((await b.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/{action}",new{confirmar=true,data=date,horario="10:00"})).StatusCode==HttpStatusCode.NotFound,"Paciente não pode "+action+" consulta alheia");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/confirmar",new{confirmar=true})).IsSuccessStatusCode,"Confirmação nativa usa serviço clínico");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/remarcar",new{confirmar=true,data=date,horario="10:00"})).IsSuccessStatusCode,"Remarcação nativa usa disponibilidade real");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/cancelar",new{confirmar=true})).IsSuccessStatusCode,"Cancelamento nativo libera consulta própria");
            var consultations=await Json(await a.GetAsync("/api/mobile/v1/consultas"));check(consultations.GetArrayLength()==1&&consultations[0].GetProperty("status").GetString()=="Cancelada","Lista móvel não expõe consulta do outro paciente");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Quero uma informação de teste"})).IsSuccessStatusCode,"Chat nativo usa fluxo existente com IA simulada");
            var hist=await Json(await a.GetAsync("/api/mobile/v1/atendimento/historico"));var histB=await Json(await b.GetAsync("/api/mobile/v1/atendimento/historico"));check(hist.GetProperty("mensagens").GetArrayLength()>=2&&histB.GetProperty("mensagens").GetArrayLength()==0,"Histórico do chat permanece isolado por paciente");
            var fake=(FakeAgent)app.Services.GetRequiredService<IAgenteClinicaService>();var calls=fake.Calls;
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["a"]);c.Modo=ModoAtendimento.Humano;await db.SaveChangesAsync();}
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Aguardando a equipe"})).IsSuccessStatusCode&&fake.Calls==calls,"Atendimento humano pausa respostas automáticas no app");
            var ended=await Json(await a.PostAsJsonAsync("/api/mobile/v1/atendimento/encerrar",new{}));var rating=ended.GetProperty("avaliacao").GetProperty("id").GetString();
            check((await b.PostAsJsonAsync("/api/mobile/v1/atendimento/avaliar",new{id=rating,nota=5,comentario="Alheio"})).StatusCode==HttpStatusCode.NotFound,"Feedback alheio não pode ser respondido");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/avaliar",new{id=rating,nota=5,comentario="Atendimento teste"})).IsSuccessStatusCode,"Feedback nativo é registrado ao encerrar");
            string expired;
            using(var scope=app.Services.CreateScope()){
                var options=scope.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(MobileAuth.Scheme);
                var original=options.RefreshTokenProtector.Unprotect(tokens.GetProperty("refreshToken").GetString())!;
                expired=options.RefreshTokenProtector.Protect(new AuthenticationTicket(original.Principal,new AuthenticationProperties{ExpiresUtc=DateTimeOffset.UtcNow.AddDays(-1)},MobileAuth.Scheme));
            }
            check((await a.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=expired})).StatusCode==HttpStatusCode.Unauthorized,"Refresh expirado é recusado mesmo com proteção válida");
            check((await a.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken="adulterado"})).StatusCode==HttpStatusCode.Unauthorized,"Refresh adulterado é recusado");
            var refreshed=await Json(await a.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=tokens.GetProperty("refreshToken").GetString()}));check(refreshed.GetProperty("accessToken").GetString()!=tokens.GetProperty("accessToken").GetString(),"Refresh válido renova tokens");
            check((await a.PostAsJsonAsync("/api/mobile/v1/auth/logout",new{})).IsSuccessStatusCode,"Logout móvel revoga sessão no servidor");
            check((await a.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Forbidden&&(await device2.GetAsync("/api/mobile/v1/perfil")).IsSuccessStatusCode,"Logout revoga apenas o dispositivo escolhido");
            check((await a.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=tokens.GetProperty("refreshToken").GetString()})).StatusCode==HttpStatusCode.Unauthorized,"Sessão revogada não pode renovar token");
            check((await device2.PostAsJsonAsync("/api/mobile/v1/perfil/senha",new{senhaAtual="errada",novaSenha="Nova#2026Senha",confirmarSenha="Nova#2026Senha"})).StatusCode==HttpStatusCode.BadRequest,"Troca de senha exige senha atual");
            check((await device2.PostAsJsonAsync("/api/mobile/v1/perfil/senha",new{senhaAtual=Password,novaSenha="Nova#2026Senha",confirmarSenha="Nova#2026Senha"})).IsSuccessStatusCode,"Nova senha é persistida pelo Identity");
            check((await device2.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Forbidden,"Troca de senha invalida o acesso móvel anterior");
            await Login(a,"a@mobile.test","Nova#2026Senha");
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var p=await db.Pacientes.SingleAsync(p=>p.Id==1);p.Ativo=false;await db.SaveChangesAsync();}
            check((await a.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Forbidden,"Desativação do paciente corta acesso imediatamente");
        }
        finally{await app.StopAsync();}
    }
    static void Must(IdentityResult r){if(!r.Succeeded)throw new Exception(string.Join(";",r.Errors.Select(e=>e.Description)));}
    sealed class FakeAgent:IAgenteClinicaService
    {
        public int Calls;
        public Task<AgenteResposta> EnviarAsync(string mensagem,string? sessionId,AgenteUsuarioContexto usuario,IReadOnlyList<MensagemHistoricoAgente>? historicoCliente=null,CancellationToken cancellationToken=default){ Calls++; return Task.FromResult(new AgenteResposta{Resposta="Resposta simulada para testes do aplicativo.",SessionId=sessionId??"teste"}); }
    }
}
