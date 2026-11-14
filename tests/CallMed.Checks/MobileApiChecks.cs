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
using MKSANCrud.Services.Psicologia;

internal static class MobileApiChecks
{
    const string Password="Paciente#2026";
    public static async Task RunAsync(Action<bool,string> check)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(MKSANCrud.Controllers.ConsultaController).Assembly.FullName,EnvironmentName="Development",ContentRootPath=Path.GetFullPath("src/CallMed.Web")});
        builder.Logging.ClearProviders(); var database="mobile-http-"+Guid.NewGuid();
        builder.Services.AddDbContext<MKSANContext>(o=>o.UseInMemoryDatabase(database).ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();
        builder.Services.AddIdentity<Usuario,IdentityRole>(o=>{o.User.RequireUniqueEmail=true;o.Password.RequiredLength=8;}).AddEntityFrameworkStores<MKSANContext>().AddDefaultTokenProviders();
        builder.Services.AddHttpContextAccessor();builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddCallMedMobile();
        builder.Services.AddScoped<PortalAcessoService>();builder.Services.AddScoped<UsuarioVinculoService>();builder.Services.AddScoped<ContaService>();
        builder.Services.AddSingleton<IClinicaClock,ClinicaClock>();builder.Services.AddScoped<ConvenioService>();builder.Services.AddScoped<ConvenioElegibilidadeService>();builder.Services.AddScoped<EspecialidadeService>();builder.Services.AddScoped<AgendamentoService>();builder.Services.AddScoped<MKSANCrud.Application.Queries.ConsultaListQuery>();builder.Services.AddScoped<MKSANCrud.Application.Queries.PacienteListQuery>();builder.Services.AddScoped<MKSANCrud.Application.Consultas.ConsultaWorkflow>();builder.Services.AddScoped<AuditoriaService>();builder.Services.AddScoped<SolicitacaoAtendimentoService>();
        builder.Services.AddScoped<AtendimentoIdentidadeService>();builder.Services.AddScoped<AtendimentoConversaService>();builder.Services.AddScoped<AtendimentoEnvioService>();builder.Services.AddScoped<AtendimentoFeedbackService>();builder.Services.AddScoped<AgenteHistoricoService>();
        builder.Services.Configure<GmailInboxOptions>(_=>{});builder.Services.AddSingleton<IAgenteClinicaService,FakeAgent>();
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("agente",x=>{x.PermitLimit=100;x.Window=TimeSpan.FromMinutes(1);}));
        builder.Services.AddScoped<JornadaService>();builder.Services.AddScoped<EncaixeService>();builder.Services.AddScoped<TeleconsultaAcessoService>();builder.Services.AddScoped<TeleconsultaPresencaService>();builder.Services.AddScoped<SalasService>();builder.Services.AddScoped<TeleconsultaIceService>();
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
                db.Disponibilidades.Add(new(){MedicoId=1,Data=day.AddDays(100),Horario="13:00",Ativo=true});
                db.ExamesHistorico.AddRange(
                    new ExameHistorico{Id=1,PacienteId=1,Nome="Aprovado próprio",Ativo=true,StatusTriagem=ExameTriagemStatus.Aprovado,DataRealizacao=day.AddDays(-15),LocalRealizacao="Clínica"},
                    new ExameHistorico{Id=2,PacienteId=1,Nome="Pendente",Ativo=true,StatusTriagem=ExameTriagemStatus.Pendente},
                    new ExameHistorico{Id=3,PacienteId=1,Nome="Rejeitado",Ativo=true,StatusTriagem=ExameTriagemStatus.Rejeitado},
                    new ExameHistorico{Id=4,PacienteId=1,Nome="Inativo",Ativo=false,StatusTriagem=ExameTriagemStatus.Aprovado},
                    new ExameHistorico{Id=5,PacienteId=2,Nome="Aprovado alheio",Ativo=true,StatusTriagem=ExameTriagemStatus.Aprovado});
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
            check((await cookie.GetAsync("/api/mobile/v1/clinico/100")).StatusCode==HttpStatusCode.Unauthorized,"Cookie do site não autentica preparação clínica nativa");
            check((await a.GetAsync("/api/mobile/v1/clinico/100")).StatusCode==HttpStatusCode.NotFound,"Preparação nativa não revela consulta de outro paciente");
            check((await a.GetAsync("/api/mobile/v1/clinico/100/tele")).StatusCode==HttpStatusCode.NotFound,"Sala nativa não revela consulta alheia");
            check((await a.PostAsJsonAsync("/api/mobile/v1/privacidade",new{tipo="Eliminacao",confirmar=false})).StatusCode==HttpStatusCode.BadRequest,"Pedido de privacidade exige confirmação expressa");
            var privacy=await Json(await a.PostAsJsonAsync("/api/mobile/v1/privacidade",new{tipo="Acesso",confirmar=true}));
            var privacyAgain=await Json(await a.PostAsJsonAsync("/api/mobile/v1/privacidade",new{tipo="Acesso",confirmar=true}));
            check(privacy.GetProperty("id").GetString()==privacyAgain.GetProperty("id").GetString(),"Pedido de privacidade pendente reaproveita protocolo");
            check((await Json(await a.GetAsync("/api/mobile/v1/privacidade"))).GetArrayLength()==1&&(await Json(await b.GetAsync("/api/mobile/v1/privacidade"))).GetArrayLength()==0,"Pedidos e respostas de privacidade pertencem somente ao titular autenticado");
            check((await cookie.GetAsync("/api/mobile/v1/privacidade")).StatusCode==HttpStatusCode.Unauthorized,"Privacidade móvel recusa cookies web");
            var profile=await Json(await a.GetAsync("/api/mobile/v1/perfil?usuarioId="+ids["b"]));check(profile.GetProperty("nome").GetString()=="Paciente a","Perfil ignora identificador de outro usuário");
            var update=await a.PutAsJsonAsync("/api/mobile/v1/perfil",new{nome="Alex Atualizado",telefone="5579999990000",dataNascimento="2000-01-01",temConvenio=false,cpf="00000000000",usuarioId=ids["b"],email="troca@test.invalid",papel="Admin"});
            profile=await Json(update);check(profile.GetProperty("cpf").GetString()=="52998224725"&&profile.GetProperty("email").GetString()=="a@mobile.test","Atualização móvel protege CPF, e-mail, papel e vínculo");
            check((await a.PutAsJsonAsync("/api/mobile/v1/perfil",new{nome="Futuro",dataNascimento="2999-01-01"})).StatusCode==HttpStatusCode.BadRequest,"Perfil recusa nascimento futuro");
            check((await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{tema="arbitrario"})).StatusCode==HttpStatusCode.BadRequest,"Preferências recusam tema inválido");
            var pref=await Json(await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{tema="light",avisosWhatsApp=false,avisosEmail=true,avisosSms=false,canalPreferido="Email"}));check(pref.GetProperty("tema").GetString()=="light"&&!pref.GetProperty("avisosSms").GetBoolean(),"Preferências móveis persistem na conta compartilhada");
            var leitura=await Json(await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{textoMaior=true,altoContraste=true,modoSimplificado=true,reduzirAnimacoes=true}));
            check(leitura.GetProperty("textoMaior").GetBoolean()&&leitura.GetProperty("altoContraste").GetBoolean()&&leitura.GetProperty("modoSimplificado").GetBoolean()&&leitura.GetProperty("reduzirAnimacoes").GetBoolean(),"Leitura acessível é persistida nos mesmos campos do site");
            check(leitura.GetProperty("avisosEmail").GetBoolean()&&!leitura.GetProperty("avisosSms").GetBoolean()&&leitura.GetProperty("tema").GetString()=="light","Salvar leitura não altera tema ou avisos omitidos no pedido");
            leitura=await Json(await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{avisosSms=true}));
            check(leitura.GetProperty("altoContraste").GetBoolean()&&leitura.GetProperty("textoMaior").GetBoolean(),"Aplicativo antigo e avisos preservam preferências de acessibilidade");
            check((await a.PutAsJsonAsync("/api/mobile/v1/perfil/preferencias",new{tema=""})).StatusCode==HttpStatusCode.BadRequest,"Tema vazio é recusado sem sobrescrever a preferência válida");
            var exams=await Json(await a.GetAsync("/api/mobile/v1/exames?pacienteId=2"));
            check(exams.GetArrayLength()==1&&exams[0].GetProperty("id").GetInt32()==1,"Exames móveis mostram apenas próprios, ativos e aprovados");
            check((await Json(await b.GetAsync("/api/mobile/v1/exames")))[0].GetProperty("id").GetInt32()==5,"Outro paciente recebe somente o próprio histórico de exames");
            check((await a.GetAsync("/api/mobile/v1/exames/5")).StatusCode==HttpStatusCode.NotFound,"ID no caminho não expõe exame de outro paciente");
            using(var photo=new MultipartFormDataContent()){photo.Add(new ByteArrayContent(ContaPerfilChecks.Png),"foto","foto.png");check((await a.PostAsync("/api/mobile/v1/perfil/foto",photo)).IsSuccessStatusCode,"Upload nativo aceita foto válida");}
            check((await a.GetByteArrayAsync("/api/mobile/v1/perfil/foto")).SequenceEqual(ContaPerfilChecks.Png)&&(await b.GetAsync("/api/mobile/v1/perfil/foto?usuarioId="+ids["a"])).StatusCode==HttpStatusCode.NotFound,"Foto móvel é restrita à própria conta");
            var doctors=await Json(await a.GetAsync("/api/mobile/v1/medicos"));check(doctors.GetArrayLength()==1,"Catálogo nativo omite médico inativo");
            check((await a.GetAsync("/api/mobile/v1/medicos/1/datas?consultaId=100")).StatusCode==HttpStatusCode.NotFound,"Disponibilidade não permite ignorar consulta alheia");
            check((await Json(await a.GetAsync("/api/mobile/v1/medicos/1/datas"))).EnumerateArray().Any(x=>x.GetString()==date),"Datas móveis vêm da agenda real");
            var allDates=await Json(await a.GetAsync("/api/mobile/v1/medicos/1/datas"));
            check(allDates.EnumerateArray().Any(x=>x.GetString()==day.AddDays(100).ToString("yyyy-MM-dd")),"Calendário móvel usa as datas do site sem corte próprio de 90 dias");
            var first=await Json(await a.GetAsync("/api/mobile/v1/agenda/primeira-vaga?especialidade=Cl%C3%ADnica%20geral"));
            check(first.GetProperty("medicoId").GetInt32()==1&&first.GetProperty("data").GetString()==date,"Primeira vaga reaproveita a busca de especialidade do site");
            check((await a.GetAsync("/api/mobile/v1/agenda/primeira-vaga?especialidade=Inexistente")).StatusCode==HttpStatusCode.NotFound,"Especialidade sem vaga não gera horário fictício");
            var eligibility=await Json(await a.GetAsync("/api/mobile/v1/medicos/1/elegibilidade"));
            check(!eligibility.GetProperty("possuiConvenioValido").GetBoolean(),"Revisão do pagamento consulta a elegibilidade administrativa do titular");
            object Booking(bool confirm=true,string hour="09:00",string payment="Particular")=>new{medicoId=1,data=date,horario=hour,pagamento=payment,confirmar=confirm,pacienteId=2};
            check((await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking(false))).StatusCode==HttpStatusCode.BadRequest,"Agendamento exige confirmação explícita");
            check(!(await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking(true,"09:00","Convenio"))).IsSuccessStatusCode,"Agendamento não aceita convênio inexistente");
            var booked=await Json(await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking()));var id=booked.GetProperty("consulta").GetProperty("id").GetInt32();
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var psych=await db.Psicologos.FindAsync(1);psych!.Crp="15/88888";psych.RegistroVerificadoEm=DateTime.UtcNow;
                db.Jornadas.Add(new(){ConsultaId=id,Tele=true});db.FormulariosClinicos.Add(new(){MedicoId=1,Titulo="Preparação fictícia",Perguntas="[\"Como prefere ser chamado?\"]",Versao=1});await db.SaveChangesAsync();
            }
            var clinical=await Json(await a.GetAsync($"/api/mobile/v1/clinico/{id}"));
            check(clinical.GetProperty("usuarioId").GetString()==ids["a"]&&clinical.GetProperty("formulario").GetProperty("titulo").GetString()=="Preparação fictícia","App lê perguntas reais e identifica a própria chave clínica");
            check((await a.GetAsync($"/api/mobile/v1/clinico/{id}/registros")).IsSuccessStatusCode,"App consulta registros disponíveis do próprio prontuário");
            var tele=await Json(await a.GetAsync($"/api/mobile/v1/clinico/{id}/tele"));
            check(tele.GetProperty("estado").GetString()=="consentimento"&&tele.GetProperty("podeConsentir").GetBoolean(),"Sala nativa explica necessidade de consentimento");
            check((await a.GetAsync($"/api/mobile/v1/clinico/{id}/tele/config")).StatusCode==HttpStatusCode.Conflict,"Bloqueio de horário ou consentimento não expulsa o paciente da conta");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/clinico/{id}/tele/consentimento",new{confirmar=true,versao="antiga"})).StatusCode==HttpStatusCode.BadRequest,"Sala nativa recusa aceite de termo antigo");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/clinico/{id}/tele/consentimento",new{confirmar=true,versao=ConsentimentoPsicologia.Versao})).IsSuccessStatusCode,"Consentimento nativo usa o mesmo termo auditado do site");
            tele=await Json(await a.GetAsync($"/api/mobile/v1/clinico/{id}/tele"));
            check(tele.GetProperty("estado").GetString()=="aguardando-horario"&&!tele.GetProperty("liberada").GetBoolean(),"Consentimento não contorna janela de entrada na chamada");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/clinico/{id}/tele/sinais",new{payload="{}"})).StatusCode==HttpStatusCode.Conflict,"Sinalização nativa permanece bloqueada fora da janela");
            var again=await Json(await a.PostAsJsonAsync("/api/mobile/v1/consultas",Booking()));check(again.GetProperty("consulta").GetProperty("id").GetInt32()==id,"Repetição do mesmo agendamento não duplica consulta");
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();
                check(await db.SolicitacoesAtendimento.CountAsync(x=>x.ConsultaId==id&&x.PacienteId==1&&x.Status==StatusSolicitacaoAtendimento.Confirmada)==1,"Agendamento nativo vincula uma solicitação confirmada como o site");
                check(await db.AuditoriaEventos.CountAsync(x=>x.Entidade=="Consulta"&&x.EntidadeId==id.ToString()&&x.Acao=="Agendar"&&x.UsuarioId==ids["a"])==1,"Repetição não duplica auditoria de agendamento nem perde autoria");
            }
            check(!(await b.PostAsJsonAsync("/api/mobile/v1/consultas",Booking())).IsSuccessStatusCode,"Outro paciente não ocupa horário já reservado");
            foreach(var action in new[]{"cancelar","confirmar","remarcar"})check((await b.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/{action}",new{confirmar=true,data=date,horario="10:00"})).StatusCode==HttpStatusCode.NotFound,"Paciente não pode "+action+" consulta alheia");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/confirmar",new{confirmar=true})).IsSuccessStatusCode,"Confirmação nativa usa serviço clínico");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/remarcar",new{confirmar=true,data=date,horario="10:00"})).IsSuccessStatusCode,"Remarcação nativa usa disponibilidade real");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/consultas/{id}/cancelar",new{confirmar=true})).IsSuccessStatusCode,"Cancelamento nativo libera consulta própria");
            var consultations=await Json(await a.GetAsync("/api/mobile/v1/consultas"));check(consultations.GetArrayLength()==1&&consultations[0].GetProperty("status").GetString()=="Cancelada","Lista móvel não expõe consulta do outro paciente");
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();
                check((await db.SolicitacoesAtendimento.SingleAsync(x=>x.ConsultaId==id)).Status==StatusSolicitacaoAtendimento.Cancelada,"Cancelamento nativo sincroniza a solicitação existente");
                check(await db.AuditoriaEventos.AnyAsync(x=>x.Entidade=="Consulta"&&x.EntidadeId==id.ToString()&&x.Acao=="Remarcar"),"Remarcação nativa mantém o registro de auditoria");
                db.Consultas.Add(new(){Id=300,PacienteId=1,MedicoId=1,Data=day.AddDays(-11),Horario="08:00",Status=ConsultaStatus.Pendente});await db.SaveChangesAsync();
            }
            var past=(await Json(await a.GetAsync("/api/mobile/v1/consultas"))).EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==300);
            check(!past.GetProperty("futura").GetBoolean()&&past.GetProperty("podeRemarcar").GetBoolean(),"Histórico distingue data passada da permissão de remarcação do site");
            check((await a.PostAsJsonAsync("/api/mobile/v1/consultas/300/remarcar",new{confirmar=true,data=date,horario="11:00"})).IsSuccessStatusCode,"Remarcação de pendência passada segue a mesma regra do serviço web");
            using var b2=Client();var b2Tokens=await Login(b2,"b@mobile.test");
            var sessions=await Json(await b.GetAsync("/api/mobile/v1/perfil/sessoes"));var ownSession=sessions.EnumerateArray().Single(x=>x.GetProperty("atual").GetBoolean()).GetProperty("id").GetString();var otherSession=sessions.EnumerateArray().First(x=>!x.GetProperty("atual").GetBoolean()).GetProperty("id").GetString();
            check(sessions.GetArrayLength()==2,"Sessões nativas mostram os acessos ativos do titular");
            check((await a.PostAsJsonAsync($"/api/mobile/v1/perfil/sessoes/{otherSession}/encerrar",new{confirmar=true})).StatusCode==HttpStatusCode.NotFound,"Gerenciamento não permite revogar acesso alheio");
            check((await b.PostAsJsonAsync($"/api/mobile/v1/perfil/sessoes/{ownSession}/encerrar",new{confirmar=true})).StatusCode==HttpStatusCode.BadRequest,"Encerramento individual protege o acesso atual");
            check((await b.PostAsJsonAsync("/api/mobile/v1/perfil/sessoes/encerrar-outras",new{confirmar=false})).StatusCode==HttpStatusCode.BadRequest&&(await b2.GetAsync("/api/mobile/v1/perfil")).IsSuccessStatusCode,"Encerrar outros acessos exige confirmação explícita");
            check((await b.PostAsJsonAsync("/api/mobile/v1/perfil/sessoes/encerrar-outras",new{confirmar=true})).IsSuccessStatusCode&&(await b2.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized&&(await b.GetAsync("/api/mobile/v1/perfil")).IsSuccessStatusCode,"Revogação mantém o dispositivo atual e invalida os demais");
            using var b3=Client();var b3Tokens=await Login(b3,"b@mobile.test");
            check((await b.PutAsJsonAsync("/api/mobile/v1/perfil/email",new{novoEmail="novo-b@mobile.test",senhaAtual="errada"})).StatusCode==HttpStatusCode.BadRequest,"Troca de e-mail exige senha atual");
            check((await b.PutAsJsonAsync("/api/mobile/v1/perfil/email",new{novoEmail="a@mobile.test",senhaAtual=Password})).StatusCode==HttpStatusCode.Conflict,"Troca de e-mail recusa conta já cadastrada");
            check((await b.PutAsJsonAsync("/api/mobile/v1/perfil/email",new{novoEmail="invalido",senhaAtual=Password})).StatusCode==HttpStatusCode.BadRequest,"Troca de e-mail valida formato antes de gravar");
            var emailResponse=await b.PutAsJsonAsync("/api/mobile/v1/perfil/email",new{novoEmail="novo-b@mobile.test",senhaAtual=Password});var emailTokens=await Json(emailResponse);
            check(!emailResponse.Headers.Contains("Set-Cookie")&&emailTokens.GetProperty("tokenType").GetString()=="Bearer","Troca de e-mail renova a identidade móvel sem cookie web");
            check((await b.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized,"Stamp antigo deixa de autorizar após alterar e-mail");
            b.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",emailTokens.GetProperty("accessToken").GetString());
            check((await Json(await b.GetAsync("/api/mobile/v1/perfil"))).GetProperty("email").GetString()=="novo-b@mobile.test","Novo token mantém acesso ao perfil após alterar e-mail");
            check((await b3.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized,"Alterar e-mail encerra os outros dispositivos");
            var currentSession=(await Json(await b.GetAsync("/api/mobile/v1/perfil/sessoes"))).EnumerateArray().Single(x=>x.GetProperty("atual").GetBoolean()).GetProperty("id").GetString();
            check(currentSession==ownSession,"Renovação da identidade mantém a sessão atual em vez de criar outro dispositivo");
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var u=await db.Users.SingleAsync(x=>x.Id==ids["b"]);var patientB=await db.Pacientes.SingleAsync(x=>x.Id==2);
                check(u.Email=="novo-b@mobile.test"&&u.UserName==u.Email&&patientB.Email==u.Email,"Identity, login e cadastro clínico mantêm o mesmo e-mail");
            }
            await Login(b2,"111.444.777-35");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Quero uma informação de teste"})).IsSuccessStatusCode,"Chat nativo usa fluxo existente com IA simulada");
            var hist=await Json(await a.GetAsync("/api/mobile/v1/atendimento/historico"));var histB=await Json(await b.GetAsync("/api/mobile/v1/atendimento/historico"));check(hist.GetProperty("mensagens").GetArrayLength()>=2&&histB.GetProperty("mensagens").GetArrayLength()==0,"Histórico do chat permanece isolado por paciente");
            var fake=(FakeAgent)app.Services.GetRequiredService<IAgenteClinicaService>();var calls=fake.Calls;
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["a"]);c.Modo=ModoAtendimento.Humano;await db.SaveChangesAsync();}
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Aguardando a equipe"})).IsSuccessStatusCode&&fake.Calls==calls,"Atendimento humano pausa respostas automáticas no app");
            var ended=await Json(await a.PostAsJsonAsync("/api/mobile/v1/atendimento/encerrar",new{}));var rating=ended.GetProperty("avaliacao").GetProperty("id").GetString();
            var closedHistory=await Json(await a.GetAsync("/api/mobile/v1/atendimento/historico"));
            check(closedHistory.GetProperty("encerrada").GetBoolean()&&closedHistory.GetProperty("avaliacao").GetProperty("id").GetString()==rating,"Histórico mostra encerramento e avaliação pendente sem reabrir a conversa");
            check(closedHistory.GetProperty("historicoVersao").GetInt32()==2&&closedHistory.GetProperty("mensagens").GetArrayLength()==0&&closedHistory.GetProperty("haAnteriores").GetBoolean(),"Atendimento encerrado sai da conversa atual e fica disponível em Anteriores");
            var archive=await Json(await a.GetAsync("/api/mobile/v1/atendimento/anteriores"));
            check(archive.GetProperty("itens").GetArrayLength()==1&&archive.GetProperty("itens")[0].GetProperty("id").GetString()==rating,"Histórico lista os atendimentos encerrados do titular");
            var detail=await Json(await a.GetAsync($"/api/mobile/v1/atendimento/anteriores/{rating}"));
            check(detail.GetProperty("mensagens").GetArrayLength()>=2&&detail.GetProperty("mensagens").EnumerateArray().Any(m=>m.GetProperty("texto").GetString()=="Quero uma informação de teste"),"Conversa arquivada mantém as mensagens anteriores consultáveis");
            check((await b.GetAsync($"/api/mobile/v1/atendimento/anteriores/{rating}")).StatusCode==HttpStatusCode.NotFound,"Detalhe de atendimento anterior não pode ser lido por outra conta");
            check((await b.GetAsync($"/api/mobile/v1/atendimento/anteriores?antesDe={rating}")).StatusCode==HttpStatusCode.NotFound,"Cursor de paginação de outra conta é rejeitado");
            check((await b.PostAsJsonAsync("/api/mobile/v1/atendimento/avaliar",new{id=rating,nota=5,comentario="Alheio"})).StatusCode==HttpStatusCode.NotFound,"Feedback alheio não pode ser respondido");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/avaliar",new{id=rating,nota=5,comentario="Atendimento teste"})).IsSuccessStatusCode,"Feedback nativo é registrado ao encerrar");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="oi"})).IsSuccessStatusCode,"Nova saudação é aceita após avaliar um atendimento encerrado");
            var reopened=await Json(await a.GetAsync("/api/mobile/v1/atendimento/historico"));
            check(!reopened.GetProperty("encerrada").GetBoolean()&&reopened.GetProperty("avaliacao").ValueKind==JsonValueKind.Null&&reopened.GetProperty("mensagens").EnumerateArray().Any(m=>m.GetProperty("texto").GetString()=="oi"),"Nova mensagem reabre o atendimento, preserva o histórico e remove o encerramento antigo");
            check(reopened.GetProperty("mensagens").GetArrayLength()==2&&!reopened.GetProperty("mensagens").EnumerateArray().Any(m=>m.GetProperty("texto").GetString()=="Quero uma informação de teste"),"Novo atendimento mostra somente suas mensagens, sem misturar a conversa encerrada");
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["a"]);c.Ativa=false;await db.SaveChangesAsync();}
            var repaired=await Json(await a.GetAsync("/api/mobile/v1/atendimento/historico"));
            check(!repaired.GetProperty("encerrada").GetBoolean()&&repaired.GetProperty("avaliacao").ValueKind==JsonValueKind.Null&&repaired.GetProperty("mensagens").GetArrayLength()==2,"Entrada posterior ao último encerramento não recebe aviso antigo mesmo com flag legada desatualizada");
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["a"]);c.Ativa=true;await db.SaveChangesAsync();}
            var nextEnded=await Json(await a.PostAsJsonAsync("/api/mobile/v1/atendimento/encerrar",new{}));var pendingRating=nextEnded.GetProperty("avaliacao").GetProperty("id").GetString();
            check(pendingRating!=rating,"Um novo atendimento encerrado tem sua própria avaliação");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Quero continuar, ainda tenho uma dúvida"})).IsSuccessStatusCode,"Avaliação pendente não bloqueia uma nova pergunta");
            var resumed=await Json(await a.GetAsync("/api/mobile/v1/atendimento/historico"));
            check(!resumed.GetProperty("encerrada").GetBoolean()&&resumed.GetProperty("avaliacao").ValueKind==JsonValueKind.Null,"Ao retomar sem avaliar, o histórico não oferece a avaliação do atendimento anterior");
            var previousDetail=await Json(await a.GetAsync($"/api/mobile/v1/atendimento/anteriores/{rating}"));
            check(!previousDetail.GetProperty("mensagens").EnumerateArray().Any(m=>m.GetProperty("texto").GetString()=="oi"),"Arquivo do primeiro atendimento não incorpora mensagens de atendimentos posteriores");
            check((await a.PostAsJsonAsync("/api/mobile/v1/atendimento/iniciar",new{confirmarEncerramento=false})).StatusCode==HttpStatusCode.BadRequest,"Começar outro atendimento exige confirmar o encerramento do atual");
            var freshChat=await Json(await a.PostAsJsonAsync("/api/mobile/v1/atendimento/iniciar",new{confirmarEncerramento=true}));
            check(!freshChat.GetProperty("encerrada").GetBoolean()&&freshChat.GetProperty("mensagens").GetArrayLength()==0&&freshChat.GetProperty("avaliacao").ValueKind==JsonValueKind.Null,"Iniciar novo atendimento arquiva o atual e abre conversa vazia");
            var allArchives=await Json(await a.GetAsync("/api/mobile/v1/atendimento/anteriores"));var archiveCount=allArchives.GetProperty("itens").GetArrayLength();
            await Json(await a.PostAsJsonAsync("/api/mobile/v1/atendimento/iniciar",new{confirmarEncerramento=false}));
            var repeatedStart=await Json(await a.GetAsync("/api/mobile/v1/atendimento/anteriores"));
            check(repeatedStart.GetProperty("itens").GetArrayLength()==archiveCount,"Abrir novamente uma conversa vazia não cria arquivo ou pesquisa duplicada");
            fake.Before=async()=>{using var scope=app.Services.CreateScope();var svc=scope.ServiceProvider.GetRequiredService<AtendimentoConversaService>();var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["a"]);await svc.EncerrarAsync(c);};
            var lateReply=await a.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Pergunta em geração durante encerramento"});fake.Before=null;
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();check(lateReply.StatusCode==HttpStatusCode.Conflict&&!await db.MensagensAtendimento.AnyAsync(m=>m.Texto=="Pergunta em geração durante encerramento"),"Resposta comum da IA concluída após encerramento não reabre nem mistura o atendimento finalizado");}
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["b"]);
                for(var i=0;i<260;i++)db.MensagensAtendimento.Add(new(){ConversaAtendimentoId=c.Id,Direcao=i%2==0?DirecaoMensagemAtendimento.Entrada:DirecaoMensagemAtendimento.Saida,Autor=i%2==0?AutorMensagemAtendimento.Paciente:AutorMensagemAtendimento.Assistente,Status=StatusMensagemAtendimento.Enviada,Texto="Histórico preservado "+i});
                await db.SaveChangesAsync();c.Ativa=false;await db.SaveChangesAsync();
            }
            var legacy=await Json(await b.GetAsync("/api/mobile/v1/atendimento/historico"));
            check(legacy.GetProperty("encerrada").GetBoolean()&&legacy.GetProperty("mensagens").GetArrayLength()==0&&legacy.GetProperty("avaliacao").ValueKind==JsonValueKind.Null,"Fechamento legado é arquivado sem solicitar avaliação retroativa");
            var legacyArchives=await Json(await b.GetAsync("/api/mobile/v1/atendimento/anteriores"));var legacyId=legacyArchives.GetProperty("itens")[0].GetProperty("id").GetString();
            var page1=await Json(await b.GetAsync($"/api/mobile/v1/atendimento/anteriores/{legacyId}"));var before=page1.GetProperty("mensagens")[0].GetProperty("id").GetInt64();
            var page2=await Json(await b.GetAsync($"/api/mobile/v1/atendimento/anteriores/{legacyId}?antesDeMensagemId={before}"));
            check(page1.GetProperty("haMaisMensagens").GetBoolean()&&page1.GetProperty("mensagens").GetArrayLength()==60&&page2.GetProperty("mensagens").EnumerateArray().All(m=>m.GetProperty("id").GetInt64()<before),"Mensagens de um arquivo longo são paginadas sem repetição");
            await Json(await b.PostAsJsonAsync("/api/mobile/v1/atendimento/mensagens",new{mensagem="Uma nova conversa depois de um arquivo longo"}));
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var c=await db.ConversasAtendimento.SingleAsync(c=>c.Canal==CanalAtendimento.Web&&c.IdentificadorExterno==ids["b"]);check(await db.MensagensAtendimento.CountAsync(m=>m.ConversaAtendimentoId==c.Id)==262,"Novas mensagens Web/app não apagam o histórico arquivado ao ultrapassar 250 mensagens");}
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
            check((await a.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized&&(await device2.GetAsync("/api/mobile/v1/perfil")).IsSuccessStatusCode,"Logout revoga apenas o dispositivo escolhido");
            check((await a.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=tokens.GetProperty("refreshToken").GetString()})).StatusCode==HttpStatusCode.Unauthorized,"Sessão revogada não pode renovar token");
            using(var replay=Client())
            {
                var issued=await Login(replay);
                var next=await Json(await replay.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=issued.GetProperty("refreshToken").GetString()}));
                check((await replay.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=issued.GetProperty("refreshToken").GetString()})).StatusCode==HttpStatusCode.Unauthorized,"A06: reutilizar refresh anterior é recusado por HTTP");
                check((await replay.PostAsJsonAsync("/api/mobile/v1/auth/refresh",new{refreshToken=next.GetProperty("refreshToken").GetString()})).StatusCode==HttpStatusCode.Unauthorized,"A06: reutilização revoga também o refresh sucessor");
            }
            check((await device2.PostAsJsonAsync("/api/mobile/v1/perfil/senha",new{senhaAtual="errada",novaSenha="Nova#2026Senha",confirmarSenha="Nova#2026Senha"})).StatusCode==HttpStatusCode.BadRequest,"Troca de senha exige senha atual");
            var passwordResponse=await device2.PostAsJsonAsync("/api/mobile/v1/perfil/senha",new{senhaAtual=Password,novaSenha="Nova#2026Senha",confirmarSenha="Nova#2026Senha"});
            check(passwordResponse.IsSuccessStatusCode,"Nova senha é persistida pelo Identity");
            var passwordTokens=await Json(passwordResponse);using var freshPassword=Client();freshPassword.DefaultRequestHeaders.Authorization=new("Bearer",passwordTokens.GetProperty("accessToken").GetString());
            check((await freshPassword.GetAsync("/api/mobile/v1/perfil")).IsSuccessStatusCode,"Troca de senha mantém o acesso atual com tokens renovados");
            check((await device2.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized,"Troca de senha invalida o acesso móvel anterior");
            await Login(a,"a@mobile.test","Nova#2026Senha");
            using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();var p=await db.Pacientes.SingleAsync(p=>p.Id==1);p.Ativo=false;await db.SaveChangesAsync();}
            check((await a.GetAsync("/api/mobile/v1/perfil")).StatusCode==HttpStatusCode.Unauthorized,"Desativação do paciente corta acesso imediatamente");
        }
        finally{await app.StopAsync();}
    }
    static void Must(IdentityResult r){if(!r.Succeeded)throw new Exception(string.Join(";",r.Errors.Select(e=>e.Description)));}
    sealed class FakeAgent:IAgenteClinicaService
    {
        public int Calls;
        public Func<Task>? Before;
        public async Task<AgenteResposta> EnviarAsync(string mensagem,string? sessionId,AgenteUsuarioContexto usuario,IReadOnlyList<MensagemHistoricoAgente>? historicoCliente=null,CancellationToken cancellationToken=default){ Calls++;if(Before is not null)await Before();return new AgenteResposta{Resposta="Resposta simulada para testes do aplicativo.",SessionId=sessionId??"teste"}; }
    }
}
