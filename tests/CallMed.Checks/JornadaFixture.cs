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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
internal static class JornadaFixture {public static async Task RunAsync(bool piloto=false,bool amplo=false,bool salas=false,bool perfilConsultas=false){
        if(piloto){System.Globalization.CultureInfo.DefaultThreadCurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("pt-BR");System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo("pt-BR");}
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(MKSANCrud.Controllers.ConsultaController).Assembly.FullName,EnvironmentName="Development",ContentRootPath=Path.GetFullPath("src/CallMed.Web")});
        builder.Logging.ClearProviders(); var database="jornada-fixture-"+Guid.NewGuid();
        builder.Services.AddDbContext<MKSANContext>(o=>o.UseInMemoryDatabase(database).ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();
        builder.Services.AddIdentity<Usuario,IdentityRole>(o=>{o.User.RequireUniqueEmail=true;o.Password.RequiredLength=8;}).AddEntityFrameworkStores<MKSANContext>().AddDefaultTokenProviders();
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("password-recovery",x=>{x.PermitLimit=10;x.Window=TimeSpan.FromMinutes(15);}));builder.Services.AddScoped<PasswordRecoveryThrottle>();builder.Configuration["PublicOrigin"]="https://callmed.test";builder.Services.AddHttpContextAccessor();builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddCallMedMobile();
        builder.Services.AddScoped<PortalAcessoService>();builder.Services.AddScoped<UsuarioVinculoService>();builder.Services.AddScoped<ContaService>();
        builder.Services.AddSingleton<IClinicaClock,ClinicaClock>();builder.Services.AddScoped<ConvenioService>();builder.Services.AddScoped<ConvenioElegibilidadeService>();builder.Services.AddScoped<EspecialidadeService>();builder.Services.AddScoped<AgendamentoService>();builder.Services.AddScoped<MKSANCrud.Application.Queries.ConsultaListQuery>();builder.Services.AddScoped<MKSANCrud.Application.Queries.PacienteListQuery>();builder.Services.AddScoped<MKSANCrud.Application.Consultas.ConsultaWorkflow>();builder.Services.AddScoped<AgendaMedicoService>();builder.Services.AddScoped<AuditoriaService>();builder.Services.AddScoped<SolicitacaoAtendimentoService>();
        builder.Services.AddHttpClient<GeminiClient>();builder.Services.AddScoped<AtendimentoEmailDecisaoService>();builder.Services.AddScoped<AtendimentoOrquestradorService>();builder.Services.Configure<AtendimentoEmailOptions>(_=>{});builder.Services.AddScoped<AtendimentoIdentidadeService>();builder.Services.AddScoped<AtendimentoConversaService>();builder.Services.AddScoped<AtendimentoEnvioService>();builder.Services.AddScoped<AtendimentoFeedbackService>();builder.Services.AddScoped<AgenteHistoricoService>();
        builder.Services.Configure<GmailInboxOptions>(_=>{});builder.Services.AddSingleton<IAgenteClinicaService,FakeAgent>();
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("agente",x=>{x.PermitLimit=100;x.Window=TimeSpan.FromMinutes(1);}));
        builder.Services.AddScoped<MKSANCrud.Services.Psicologia.PsicologiaService>();builder.Services.AddScoped<MKSANCrud.Services.Psicologia.PreparacaoLinkService>();builder.Services.AddScoped<MKSANCrud.Services.Psicologia.FhirPsicologiaService>();builder.Services.AddScoped<JornadaService>();builder.Services.AddScoped<OperacaoService>();builder.Services.AddScoped<PilotoService>();builder.Services.AddScoped<TeleconsultaAcessoService>();builder.Services.AddScoped<TeleconsultaPresencaService>();builder.Services.AddScoped<SalasService>();builder.Services.AddScoped<TeleconsultaIceService>();builder.Services.AddScoped<EncaixeService>();builder.Services.AddScoped<ListaEsperaService>();builder.Services.AddScoped<ConsultaLinkService>();builder.Services.AddScoped<ConfirmacoesService>();builder.Services.AddScoped<OrientacoesConsultaService>();
        builder.Services.AddAuthentication().AddCookie("CallMedClinical",o=>{o.Cookie.Name="__Secure-CallMedClinical";o.Cookie.Path="/Jornada";o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.ExpireTimeSpan=TimeSpan.FromHours(6);o.SlidingExpiration=false;o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnValidatePrincipal=async c=>{if(c.Principal==null||await c.HttpContext.RequestServices.GetRequiredService<MobileSessionService>().ValidarAsync(c.Principal,c.HttpContext.RequestAborted)==null)c.RejectPrincipal();};});
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("webhooks",x=>{x.PermitLimit=1000;x.Window=TimeSpan.FromMinutes(1);}));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{{"RENDER_EXTERNAL_URL","https://callmed.test"},{"PepIntegration:Enabled","true"},{"PepIntegration:ApiKey","test-only-pep-key-01234567890123456789"}});
        var turnFixture=Environment.GetEnvironmentVariable("CALLMED_TEST_TURN_CONFIG");
        if(turnFixture is "static" or "shared" or "broken"){
            builder.Configuration["Clinica:Tele:IceUrls:0"]="stun:stun.ficticio.invalid:80";
            builder.Configuration["Clinica:Tele:IceUrls:1"]="turn:relay.ficticio.invalid:80";
            builder.Configuration["Clinica:Tele:IceUrls:2"]="turns:relay.ficticio.invalid:443?transport=tcp";
            builder.Configuration["Clinica:Tele:TurnSecret"]="segredo-compartilhado-ficticio";
            if(turnFixture!="shared")builder.Configuration["Clinica:Tele:TurnUsername"]="usuario-TURN-ficticio";
            if(turnFixture=="static")builder.Configuration["Clinica:Tele:TurnPassword"]="credencial-TURN-ficticia";
        }
        if(piloto)builder.Services.ConfigureApplicationCookie(o=>{o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};});
        builder.Services.AddSingleton<MKSANCrud.Services.Email.IEmailService,FakeEmail>();builder.Services.AddSingleton<MKSANCrud.Services.Atendimento.Canais.Email.GmailApiClient>();builder.Services.AddHttpClient<MKSANCrud.Services.Atendimento.Canais.Sms.SmsHttpSender>();
        builder.Services.AddHttpClient();builder.Services.AddControllersWithViews();builder.Services.AddRazorPages(options=>options.Conventions.AddAreaPageRoute("Identity","/Account/Login","acesso/{perfil=paciente}"));

        await using var app=builder.Build();app.Use(async(c,next)=>{try{await next(c);}catch(Exception ex){Console.Error.WriteLine(ex);throw;}});app.UseMiddleware<MKSANCrud.Middleware.BrowserSecurityHeadersMiddleware>();app.UseStaticFiles();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();app.MapControllers();app.MapRazorPages();app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
        using(var scope=app.Services.CreateScope()){
            var sp=scope.ServiceProvider;var users=sp.GetRequiredService<UserManager<Usuario>>();var roles=sp.GetRequiredService<RoleManager<IdentityRole>>();var db=sp.GetRequiredService<MKSANContext>();
            foreach(var role in new[]{"Paciente","Medico","Psicologo","Funcionario","Admin"})await roles.CreateAsync(new(role));
            foreach(var (name,role) in new[]{("p","Paciente"),("p2","Paciente"),("m","Medico"),("rt","Psicologo"),("a","Admin"),("f","Funcionario")}){var u=new Usuario{Id=name,UserName=name+"@ficticio.invalid",Email=name+"@ficticio.invalid",EmailConfirmed=true};await users.CreateAsync(u,"Teste!2026");await users.AddToRoleAsync(u,role);}
            db.Pacientes.AddRange(new Paciente{Id=1,UsuarioId="p",Nome="Paciente Fictício",Cpf="52998224725",Telefone="5579999990000",DataNascimento=new(2000,1,1)},new Paciente{Id=2,UsuarioId="p2",Nome="Outro Fictício",Cpf="11144477735",Telefone="5579999990001",DataNascimento=new(2000,1,1)});
            db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});db.Medicos.Add(new(){Crp="12/12345",RegistroVerificadoEm=DateTime.UtcNow,Id=1,UsuarioId="m",Nome="Dra. Fictícia",EspecialidadeId=1,Especialidade="Clínica geral",Crm="CRM-TESTE"});db.Medicos.Add(new(){Id=2,UsuarioId="rt",Nome="Responsável Técnico Fictício",Crp="12/67890",RegistroVerificadoEm=DateTime.UtcNow,ResponsavelTecnico=true,EspecialidadeId=1});db.Funcionarios.Add(new(){Id=1,UsuarioId="f",Nome="Recepção Fictícia",Email="f@ficticio.invalid"});
            var clock=sp.GetRequiredService<IClinicaClock>();var start=clock.Agora.AddMinutes(5);db.Consultas.Add(new(){Id=1,PacienteId=1,MedicoId=1,Data=start.Date,Horario=start.ToString("HH:mm"),Status=ConsultaStatus.Confirmada});db.Salas.Add(new(){Id=1,Nome="Sala 1"});db.Jornadas.Add(new(){ConsultaId=1,Tele=true,ConsentimentoVersao=MKSANCrud.Services.Psicologia.ConsentimentoPsicologia.Versao,ConsentimentoEm=DateTime.UtcNow,ChegadaEm=DateTime.UtcNow});db.FormulariosClinicos.Add(new(){Id=1,MedicoId=1,Versao=1,Titulo="Preparação",Perguntas="[\"Qual é o motivo da consulta?\",\"Usa algum medicamento?\"]"});
            foreach(var (id,minutes,online,consent,status) in new[]{(2,120,true,true,ConsultaStatus.Confirmada),(3,5,true,false,ConsultaStatus.Confirmada),(4,-310,true,true,ConsultaStatus.Confirmada),(5,5,false,false,ConsultaStatus.Confirmada),(6,5,true,true,ConsultaStatus.Cancelada)}){
                var at=clock.Agora.AddMinutes(minutes);db.Consultas.Add(new(){Id=id,PacienteId=1,MedicoId=1,Data=at.Date,Horario=at.ToString("HH:mm"),Status=status});
                db.Jornadas.Add(new(){ConsultaId=id,Tele=online,ConsentimentoVersao=consent?MKSANCrud.Services.Psicologia.ConsentimentoPsicologia.Versao:null,ConsentimentoEm=consent?DateTime.UtcNow:null});
            }
            if(piloto){var dia=clock.Hoje.AddDays(-7);foreach(var (id,status,duracao) in new[]{(101,ConsultaStatus.Realizada,50),(102,ConsultaStatus.Ausente,45),(103,ConsultaStatus.Cancelada,40),(104,ConsultaStatus.Pendente,20)})db.Consultas.Add(new(){Id=id,PacienteId=1,MedicoId=1,Data=dia,Horario="10:00",Status=status,DuracaoMinutos=duracao});db.Jornadas.Add(new(){ConsultaId=101,Duracao=30});}
            if(amplo){foreach(var name in new[]{"g","d"}){var u=new Usuario{Id=name,UserName=name+"@ficticio.invalid",Email=name+"@ficticio.invalid",EmailConfirmed=true};await users.CreateAsync(u,"Teste!2026");await users.AddToRoleAsync(u,"Medico");}
            db.Medicos.AddRange(new(){Id=3,Nome="Médica Fictícia",UsuarioId="g",AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="12345",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow,EspecialidadeId=1},new(){Id=4,Nome="Dentista Fictício",UsuarioId="d",AreaProfissional="Outra profissão de saúde",RegistroConselho="CRO",RegistroNumero="7890",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow,EspecialidadeId=1});
            foreach(var (id,prof) in new[]{(20,3),(21,4)}){var at=clock.Agora.AddMinutes(5);db.Consultas.Add(new(){Id=id,PacienteId=1,MedicoId=prof,Data=at.Date,Horario=at.ToString("HH:mm"),Status=ConsultaStatus.Confirmada});db.Jornadas.Add(new(){ConsultaId=id,Tele=true});}}
            if(salas){
                db.Salas.Add(new(){Id=2,Nome="Consultório 02",Localizacao="Térreo · ala B",Recursos="Acesso sem degraus, mesa e maca"});
                db.BloqueiosSala.Add(new(){SalaId=2,Inicio=clock.Hoje.AddHours(13),Fim=clock.Hoje.AddHours(14),Motivo="Manutenção preventiva"});
                foreach(var (id,prof,paciente,hour) in new[]{(30,1,1,"14:00"),(31,3,2,"15:00")}){db.Consultas.Add(new(){Id=id,PacienteId=paciente,MedicoId=prof,Data=clock.Hoje,Horario=hour,Status=ConsultaStatus.Confirmada});db.Jornadas.Add(new(){ConsultaId=id,SalaId=1,Duracao=30});}
                db.ConversasAtendimento.Add(new(){Id=100,PacienteId=1,Canal=CanalAtendimento.WhatsApp,IdentificadorExterno="fixture-whatsapp",SessionId="fixture-central",Assunto="Preparação da consulta",Modo=ModoAtendimento.Humano,ResponsavelUsuarioId="f"});
                db.MensagensAtendimento.Add(new(){ConversaAtendimentoId=100,Direcao=DirecaoMensagemAtendimento.Entrada,Autor=AutorMensagemAtendimento.Paciente,Status=StatusMensagemAtendimento.Recebida,Texto="Olá! Posso confirmar o horário da minha consulta?"});
                // V31: fictional scenarios for the actual Razor UI and state/action regression tests.
                // Only this in-memory fixture is seeded; no production data or external channel is used.
                db.MensagensAtendimento.AddRange(
                    new(){ConversaAtendimentoId=100,Direcao=DirecaoMensagemAtendimento.Saida,Autor=AutorMensagemAtendimento.Funcionario,Status=StatusMensagemAtendimento.Enviada,Texto="Olá! Sua consulta está na agenda. Você pode conferir o horário e as orientações em Minhas consultas.",CriadoEm=DateTime.UtcNow.AddSeconds(1)},
                    new(){ConversaAtendimentoId=100,Direcao=DirecaoMensagemAtendimento.Entrada,Autor=AutorMensagemAtendimento.Paciente,Status=StatusMensagemAtendimento.Recebida,Texto="Obrigada! Também consigo enviar meus exames pelo portal?",CriadoEm=DateTime.UtcNow.AddSeconds(2)});
                db.ConversasAtendimento.AddRange(
                    new(){Id=101,Canal=CanalAtendimento.Email,IdentificadorExterno="contato@exemplo.invalid",SessionId="fixture-email-review",Assunto="Orientações para a consulta",Modo=ModoAtendimento.Humano,ResponsavelUsuarioId="f",UltimaInteracaoEm=DateTime.UtcNow.AddMinutes(-4)},
                    new(){Id=102,PacienteId=2,Canal=CanalAtendimento.Web,IdentificadorExterno="fixture-site",SessionId="fixture-site-actions",Modo=ModoAtendimento.IA,UltimaInteracaoEm=DateTime.UtcNow.AddMinutes(-8)},
                    new(){Id=103,PacienteId=1,Canal=CanalAtendimento.Sms,IdentificadorExterno="fixture-sms",SessionId="fixture-sms-failure",Modo=ModoAtendimento.Humano,ResponsavelUsuarioId="f",UltimaInteracaoEm=DateTime.UtcNow.AddMinutes(-15)});
                db.MensagensAtendimento.AddRange(
                    new(){ConversaAtendimentoId=101,Direcao=DirecaoMensagemAtendimento.Entrada,Autor=AutorMensagemAtendimento.Paciente,Status=StatusMensagemAtendimento.Recebida,Texto="Quais documentos preciso levar para a primeira consulta?",CriadoEm=DateTime.UtcNow.AddMinutes(-5)},
                    new(){ConversaAtendimentoId=101,Direcao=DirecaoMensagemAtendimento.Saida,Autor=AutorMensagemAtendimento.Assistente,Status=StatusMensagemAtendimento.AguardandoAprovacao,Texto="Olá! Leve um documento de identificação e, caso utilize convênio, a carteirinha. As orientações específicas ficam na preparação da consulta.",CriadoEm=DateTime.UtcNow.AddMinutes(-4)},
                    new(){ConversaAtendimentoId=102,Direcao=DirecaoMensagemAtendimento.Entrada,Autor=AutorMensagemAtendimento.Paciente,Status=StatusMensagemAtendimento.Recebida,Texto="Gostaria de saber quais são as especialidades disponíveis.",CriadoEm=DateTime.UtcNow.AddMinutes(-8)},
                    new(){ConversaAtendimentoId=103,Direcao=DirecaoMensagemAtendimento.Entrada,Autor=AutorMensagemAtendimento.Paciente,Status=StatusMensagemAtendimento.Recebida,Texto="Onde posso acompanhar minha consulta?",CriadoEm=DateTime.UtcNow.AddMinutes(-16)},
                    new(){ConversaAtendimentoId=103,Direcao=DirecaoMensagemAtendimento.Saida,Autor=AutorMensagemAtendimento.Assistente,Status=StatusMensagemAtendimento.Falhou,Texto="Você pode acompanhar a consulta pelo portal CallMed.",Erro="Falha fictícia do canal para teste de interface.",CriadoEm=DateTime.UtcNow.AddMinutes(-15)});
            }
            if (perfilConsultas)
            {
                foreach(var prof in new[]{1,2,3,4}) foreach(var hour in new[]{"10:00","11:00"})
                    db.Disponibilidades.Add(new(){MedicoId=prof,Data=clock.Hoje.AddDays(2),Horario=hour,Ativo=true});
                db.Consultas.AddRange(
                    new() { Id=40,PacienteId=1,MedicoId=1,Data=clock.Hoje.AddDays(1),Horario="09:00",Status=ConsultaStatus.Pendente },
                    new() { Id=41,PacienteId=1,MedicoId=1,Data=clock.Hoje.AddDays(-1),Horario="09:00",Status=ConsultaStatus.Realizada,RealizadaEm=DateTime.UtcNow.AddDays(-1) },
                    new() { Id=42,PacienteId=1,MedicoId=1,Data=clock.Hoje.AddDays(-2),Horario="09:00",Status=ConsultaStatus.Cancelada },
                    new() { Id=43,PacienteId=2,MedicoId=1,Data=clock.Hoje.AddDays(1),Horario="10:00",Status=ConsultaStatus.Pendente });
                db.SolicitacoesAtendimento.AddRange(
                    new() { Id=1000,PacienteId=1,ConsultaId=40,EspecialidadeId=1,Canal=CanalAtendimento.WhatsApp,Status=StatusSolicitacaoAtendimento.Confirmada,CriadoEm=DateTime.UtcNow.AddHours(-2) },
                    new() { Id=1001,PacienteId=1,ConsultaId=40,EspecialidadeId=1,Canal=CanalAtendimento.Email,Status=StatusSolicitacaoAtendimento.Confirmada,CriadoEm=DateTime.UtcNow.AddHours(-1) },
                    new() { Id=1002,NomeContato="Pedido novo fictício",EspecialidadeId=1,Canal=CanalAtendimento.Web,Status=StatusSolicitacaoAtendimento.Nova,CriadoEm=DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }
        app.MapGet("/Jornada/fixture-expiry",(HttpContext c,IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> opts)=>{var value=c.Request.Cookies["__Secure-CallMedClinical"];var ticket=value==null?null:opts.Get("CallMedClinical").TicketDataFormat.Unprotect(value);return Results.Json(new{horas=(ticket?.Properties.ExpiresUtc-DateTimeOffset.UtcNow)?.TotalHours});});
        app.MapGet("/fixture/review",()=>Results.Content(File.ReadAllText(Path.GetFullPath("tests/browser/review.html")),"text/html"));
        app.MapGet("/fixture/axe.js",()=>Results.File(Path.GetFullPath("tests/browser/node_modules/axe-core/axe.min.js"),"text/javascript"));
        app.MapGet("/fixture/logout",async(HttpContext c)=>{await c.SignOutAsync(IdentityConstants.ApplicationScheme);return Results.Text("Sessão fictícia encerrada.");});
        app.MapGet("/fixture/debug",async(MKSANContext db)=>Results.Json(new{regs=await db.RegistrosPsicologicos.Select(r=>new{r.Id}).ToListAsync(),guardas=await db.GuardasRegistrosPsicologicos.Select(g=>new{g.RegistroId,g.PsicologoId,g.ChaveId}).ToListAsync(),psis=await db.Psicologos.Select(p=>new{p.Id,p.UsuarioId}).ToListAsync()}));
        app.MapGet("/fixture/login/{name}",async(string name,HttpContext c,SignInManager<Usuario> sign,UserManager<Usuario> users)=>{var u=await users.FindByIdAsync(name);if(u==null)return Results.NotFound();await sign.SignInAsync(u,false);return Results.Ok();});
        app.MapGet("/fixture/preparacao/{id:int}",async(int id,MKSANContext db,MKSANCrud.Services.Psicologia.PreparacaoLinkService links)=>{var c=await db.Consultas.SingleAsync(c=>c.Id==id);return Results.Json(new{url=links.Criar(c)});});
        app.MapGet("/fixture/revogar/{name}",async(string name,MKSANContext db)=>{var u=await db.Users.FindAsync(name);u!.SecurityStamp=Guid.NewGuid().ToString();await db.SaveChangesAsync();return Results.Ok();});
        app.MapGet("/fixture/link/{id:int}",async(int id,MKSANContext db,ConsultaLinkService links)=>{var c=await db.Consultas.SingleAsync(c=>c.Id==id);return Results.Json(new{url=await links.CriarAsync(c,default)});});
        app.Urls.Add(Environment.GetEnvironmentVariable("CALLMED_FIXTURE_URL") ?? "http://127.0.0.1:0");await app.StartAsync();Console.WriteLine("JORNADA_URL="+app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());await app.WaitForShutdownAsync();
}
    sealed class FakeEmail:MKSANCrud.Services.Email.IEmailService {public bool Configurado=>false;public Task<bool> EnviarAsync(string destinatario,string assunto,string corpoHtml,CancellationToken ct=default,string? replyTo=null)=>throw new InvalidOperationException("Fixture não envia e-mail real.");}
    sealed class FakeAgent:IAgenteClinicaService
    {
        public async Task<AgenteResposta> EnviarAsync(string mensagem,string? sessionId,AgenteUsuarioContexto usuario,IReadOnlyList<MensagemHistoricoAgente>? historicoCliente=null,CancellationToken cancellationToken=default){ await Task.CompletedTask;return new AgenteResposta{Resposta="Resposta simulada para testes do aplicativo.",SessionId=sessionId??"teste"}; }
    }
}
