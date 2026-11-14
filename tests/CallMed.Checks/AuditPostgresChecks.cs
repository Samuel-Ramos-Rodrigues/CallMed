using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MKSANCrud.Controllers.Mobile;
using MKSANCrud.Data;
using MKSANCrud.Extensions;
using MKSANCrud.Models;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Mobile;
using MKSANCrud.Services.Startup;
using MKSANCrud.Services.Usuarios;

internal static class AuditPostgresChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var connection=Environment.GetEnvironmentVariable("CALLMED_TEST_DB")??throw new Exception("Isolated PostgreSQL required");
        var cs=new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if(cs.Host!="127.0.0.1"||!cs.Database!.StartsWith("callmed_test"))throw new Exception("Only an isolated local database is allowed");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(MKSANCrud.Controllers.ConsultaController).Assembly.FullName,EnvironmentName="Development",ContentRootPath=Path.GetFullPath("src/CallMed.Web")});
        builder.Configuration["ConnectionStrings:MKSANContextConnection"]=connection;builder.Logging.ClearProviders();
        builder.Services.AddCallMedApplication(builder.Configuration,builder.Environment);
        await using var app=builder.Build();
        using var scope=app.Services.CreateScope();var sp=scope.ServiceProvider;var db=sp.GetRequiredService<MKSANContext>();
        await db.Database.EnsureDeletedAsync();
        await DatabaseDeployment.AplicarAsync(sp);await DatabaseDeployment.ValidarAsync(db);
        check(await db.Database.CanConnectAsync(),"A13: job cria e valida baseline completa em PostgreSQL vazio");
        db.Pacientes.Add(new(){Id=1,Nome="Paciente preservado",Cpf="52998224725",Email="pg@example.test",Ativo=false});await db.SaveChangesAsync();
        await DatabaseDeployment.AplicarAsync(sp);
        check(await db.Pacientes.CountAsync()==1&&!(await db.Pacientes.SingleAsync()).Ativo,"A13: repetir job não duplica nem reativa pacientes");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"CallMedSchemaVersions\"; ALTER TABLE \"ContaSessoes\" DROP COLUMN \"RefreshVersion\"; DROP TABLE \"SecurityBuckets\"; DROP TABLE \"AtendimentoLeases\";");
        await DatabaseDeployment.AplicarAsync(sp);await DatabaseDeployment.ValidarAsync(db);db.ChangeTracker.Clear();
        check((await db.Pacientes.SingleAsync()).Nome=="Paciente preservado","A13: upgrade de schema legado reaplica etapas versionadas sem perder cadastro");
        var cooldown=await Task.WhenAll(Enumerable.Range(0,8).Select(async i=>{
            using var another=app.Services.CreateScope();return await another.ServiceProvider.GetRequiredService<PasswordRecoveryThrottle>().PermitirAsync("CONCORRENTE@EXAMPLE.TEST","192.0.2."+i);
        }));
        check(cooldown.Count(x=>x)==1,"A05: apenas um envio por conta na janela mesmo com oito processos lógicos concorrentes");
        await using(var lease=await AtendimentoLease.AdquirirAsync(db,77,default))
        { check(lease is not null,"A17: uma instância obtém lease persistida");using var another=app.Services.CreateScope();check(await AtendimentoLease.AdquirirAsync(another.ServiceProvider.GetRequiredService<MKSANContext>(),77,default)is null,"A17: segunda instância não processa a mesma conversa simultaneamente"); }
        await using(var lease=await AtendimentoLease.AdquirirAsync(db,77,default))check(lease is not null,"A17: lease é liberada depois de concluir o processamento");
        db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});db.Medicos.Add(new(){Id=1,Nome="Profissional fictício",EspecialidadeId=1,AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="98765",RegistroUf="SE"});
        for(var n=0;n<53;n++)db.Consultas.Add(new(){PacienteId=1,MedicoId=1,Data=DateTime.Today.AddDays(n+1),Horario="10:00",Status=n%2==0?ConsultaStatus.Pendente:ConsultaStatus.Confirmada});
        await db.SaveChangesAsync();
        var linkedId=await db.Consultas.OrderBy(c=>c.Id).Skip(14).Select(c=>c.Id).FirstAsync();
        db.SolicitacoesAtendimento.Add(new(){PacienteId=1,ConsultaId=linkedId,Canal=MKSANCrud.Models.Atendimento.CanalAtendimento.WhatsApp});await db.SaveChangesAsync();
        var listing=ActivatorUtilities.CreateInstance<MKSANCrud.Controllers.ConsultaController>(sp);
        listing.ControllerContext=new(){HttpContext=new DefaultHttpContext{RequestServices=sp,User=JornadaChecks.User("staff","Funcionario")}};
        var page1=(MKSANCrud.ViewModels.SolicitacoesPainelViewModel)((ViewResult)await listing.Index(null,null,pagina:1)).Model!;
        var page2=(MKSANCrud.ViewModels.SolicitacoesPainelViewModel)((ViewResult)await listing.Index(null,null,pagina:2)).Model!;
        check(page1.TotalConsultasFiltradas==53&&page1.ConsultasDaPagina.Count==20&&page2.ConsultasDaPagina.Count==20,"A11: consulta SQL entrega uma página de 20 e preserva total de 53");
        check(page1.ConsultasDaPagina.First().Consulta!.Data==DateTime.Today.AddDays(1),"A11: incluir origem do canal não altera a ordenação SQL da página");
        check(!page1.ConsultasDaPagina.Select(x=>x.Consulta!.Id).Intersect(page2.ConsultasDaPagina.Select(x=>x.Consulta!.Id)).Any(),"A11: ordenação estável evita repetição entre páginas PostgreSQL");
        var pending=(MKSANCrud.ViewModels.SolicitacoesPainelViewModel)((ViewResult)await listing.Index(null,null,situacao:"pendentes")).Model!;
        check(pending.TotalConsultasFiltradas==27&&pending.ConsultasDaPagina.All(x=>x.Consulta!.Status==ConsultaStatus.Pendente),"A11: filtro de confirmação e contagem são executados no PostgreSQL");
        var users=sp.GetRequiredService<UserManager<Usuario>>();var roles=sp.GetRequiredService<RoleManager<IdentityRole>>();await roles.CreateAsync(new("Paciente"));
        var user=new Usuario{UserName="pg@example.test",Email="pg@example.test",EmailConfirmed=true};await users.CreateAsync(user,"Paciente#2026Senha");await users.AddToRoleAsync(user,"Paciente");
        var patient=await db.Pacientes.SingleAsync();patient.Ativo=true;patient.UsuarioId=user.Id;
        var session=new ContaSessao{UsuarioId=user.Id,Dispositivo="Audit mobile"};db.ContaSessoes.Add(session);await db.SaveChangesAsync();
        var signIn=sp.GetRequiredService<SignInManager<Usuario>>();var principal=await signIn.CreateUserPrincipalAsync(user);MobileAuth.AdicionarSessao(principal,session.Id);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(MobileAuth.RefreshVersionClaim,session.RefreshVersion.ToString()));
        var options=sp.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(MobileAuth.Scheme);
        string Token(ClaimsPrincipal p)=>options.RefreshTokenProtector.Protect(new AuthenticationTicket(p,new AuthenticationProperties{ExpiresUtc=DateTimeOffset.UtcNow.AddDays(1)},MobileAuth.Scheme));
        var old=Token(principal);
        async Task<IActionResult> Refresh(string token){using var s=app.Services.CreateScope();var ctrl=ActivatorUtilities.CreateInstance<MobileAuthController>(s.ServiceProvider);ctrl.ControllerContext=new(){HttpContext=new DefaultHttpContext{RequestServices=s.ServiceProvider}};return await ctrl.Refresh(new(){RefreshToken=token},default);}
        var first=await Refresh(old);check(first is Microsoft.AspNetCore.Mvc.SignInResult,"A06: refresh válido gira a versão da sessão no PostgreSQL");
        var nextToken=Token(((Microsoft.AspNetCore.Mvc.SignInResult)first).Principal);
        check(await Refresh(old)is UnauthorizedObjectResult,"A06: refresh anterior é recusado após a primeira renovação");
        check(await Refresh(nextToken)is UnauthorizedObjectResult,"A06: reutilização detectada revoga a família de refresh tokens");
    }
}
