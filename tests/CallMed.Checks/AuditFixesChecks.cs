using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MKSANCrud.Areas.Identity.Pages.Account;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

internal static class AuditFixesChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var root = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("audit-"+Guid.NewGuid(),root)
            .ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        var services = new ServiceCollection(); services.AddLogging(); services.AddHttpContextAccessor();
        services.AddScoped(_=>new MKSANContext(options));
        services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();
        services.AddIdentity<Usuario,IdentityRole>().AddEntityFrameworkStores<MKSANContext>().AddDefaultTokenProviders();
        services.AddSingleton<IClinicaClock,ClinicaClock>();
        var emailCapture=new AuditEmail();services.AddSingleton<MKSANCrud.Services.Email.IEmailService>(emailCapture);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PublicOrigin","https://callmed.example.test"}}).Build());
        await using var sp=services.BuildServiceProvider(); using var scope=sp.CreateScope(); var svc=scope.ServiceProvider;
        var db=svc.GetRequiredService<MKSANContext>(); var users=svc.GetRequiredService<UserManager<Usuario>>();
        var roles=svc.GetRequiredService<RoleManager<IdentityRole>>();
        foreach(var role in new[]{"Paciente","Medico","Psicologo","Admin","Funcionario"}) await roles.CreateAsync(new(role));
        var patient=new Paciente {Nome="Cadastro preservado",Cpf="52998224725",Email="presencial@example.test",Ativo=false,DataNascimento=new(2000,1,1)};
        db.Pacientes.Add(patient); await db.SaveChangesAsync();
        var http=new DefaultHttpContext {RequestServices=svc};
        var action=new ActionContext(http,new RouteData(),new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var register=new RegisterModel(users,svc.GetRequiredService<SignInManager<Usuario>>(),db,svc.GetRequiredService<IClinicaClock>())
        {PageContext=new PageContext{HttpContext=http},Url=new UrlHelper(action),Input=new RegisterModel.InputModel{Nome="Nome adulterado",Cpf=patient.Cpf,Email=patient.Email!,DataNascimento=new(2000,1,1),Password="Cadastro#2026Senha",ConfirmPassword="Cadastro#2026Senha",AceitaTermos=true}};
        check(await register.OnPostAsync("/") is PageResult&&!register.ModelState.IsValid,"A01: cadastro público recusa identidade clínica existente sem prova de posse");
        check(!await db.Users.AnyAsync(),"A01: tentativa de assumir cadastro não cria acesso Identity");
        await db.Entry(patient).ReloadAsync();
        check(!patient.Ativo&&patient.UsuarioId==null&&patient.Nome=="Cadastro preservado","A01: cadastro inativo mantém estado e dados originais");

        var user=new Usuario{UserName="profissional@example.test",Email="profissional@example.test",EmailConfirmed=true};
        await users.CreateAsync(user,"Profissional#2026Senha"); await users.AddToRolesAsync(user,["Medico","Psicologo"]);
        db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});
        var professional=new Psicologo {Nome="Profissional",Email=user.Email,UsuarioId=user.Id,Ativo=true,AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="12345",RegistroUf="SE",EspecialidadeId=1};
        db.Medicos.Add(professional);await db.SaveChangesAsync();
        var access=new MedicoAcessoController(db,users){ControllerContext=new(){HttpContext=http},Url=new UrlHelper(action),TempData=new TempDataDictionary(http,new TempData())};
        check(await access.Gerenciar(new MedicoAcessoViewModel{MedicoId=professional.Id,Email=user.Email!}) is RedirectToActionResult,"A03: gestão aceita papéis profissionais compatíveis já existentes");
        var doctor=JsonSerializer.Serialize(FhirPractitioner.Criar(professional,"urn:callmed:test"));
        check(doctor.Contains("CRM:SE")&&doctor.Contains("12345")&&!doctor.Contains("urn:callmed:crp"),"A16: Practitioner médico exporta CRM e UF, sem CRP artificial");
        professional.AreaProfissional="Psicologia";professional.Crp="19/12345";
        check(JsonSerializer.Serialize(FhirPractitioner.Criar(professional)).Contains("urn:callmed:crp"),"A16: Practitioner psicológico preserva CRP");
        professional.AreaProfissional="Medicina";professional.Crp=null;

        var canonical=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PublicOrigin","https://callmed.example.test"}}).Build();
        check(PublicOrigin.Obter(canonical).Authority=="callmed.example.test","A04: origem de recuperação depende exclusivamente da configuração");
        foreach(var invalid in new[]{"http://callmed.test","https://user:pass@callmed.test","https://callmed.test/path","https://callmed.test/?token=x"})
        {bool refused=false;try{PublicOrigin.Obter(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PublicOrigin",invalid}}).Build());}catch(InvalidOperationException){refused=true;}check(refused,"A04: recusa origem insegura ou ambígua "+invalid.Split('@').Last());}
        var patientController=new PacienteController(db,users,svc.GetRequiredService<IClinicaClock>(),new MKSANCrud.Application.Queries.PacienteListQuery(db)){ControllerContext=new(){HttpContext=http},Url=new UrlHelper(action),TempData=new TempDataDictionary(http,new TempData())};
        await patientController.EnviarConvite(patient.Id,true,default);
        check(emailCapture.Count==0&&patient.UsuarioId==null,"A01: recepção não envia convite nem reativa cadastro inativo");
        patient.Ativo=true;await db.SaveChangesAsync();
        await patientController.EnviarConvite(patient.Id,false,default);
        check(emailCapture.Count==0&&patient.UsuarioId==null,"A01: convite exige confirmação explícita da conferência de identidade");
        await patientController.EnviarConvite(patient.Id,true,default);
        var invited=await users.FindByIdAsync(patient.UsuarioId!);
        check(invited is not null&&!await users.HasPasswordAsync(invited)&&emailCapture.Count==1,"A01: convite cria acesso sem senha e entrega o link apenas ao e-mail cadastrado");
        check(emailCapture.Html.Contains("https://callmed.example.test/Identity/Account/ForgotPassword"),"A01/A04: convite usa origem HTTPS canônica");
        check(patient.Nome=="Cadastro preservado"&&patient.Cpf=="52998224725","A01: convite não substitui dados clínicos por entrada de cadastro público");
        var throttle=new PasswordRecoveryThrottle(db);
        check(await throttle.PermitirAsync("novo@example.test","192.0.2.1"),"A05: primeira solicitação legítima é permitida");
        check(!await throttle.PermitirAsync(" NOVO@EXAMPLE.TEST ","192.0.2.2"),"A05: normalização e outro IP não contornam intervalo da mesma conta");
        check(await db.SecurityBuckets.AllAsync(x=>x.Id.Length==64&&!x.Id.Contains('@')),"A05: contadores não armazenam CPF, e-mail ou IP em claro");

        var consultation=new Consulta{PacienteId=patient.Id,MedicoId=professional.Id,Data=DateTime.Today.AddDays(1),Horario="10:00",Status=ConsultaStatus.Pendente};
        db.Consultas.Add(consultation);await db.SaveChangesAsync();
        var task=new SolicitacaoAtendimento{PacienteId=patient.Id,ConsultaId=consultation.Id,Canal=CanalAtendimento.Telefone,Status=StatusSolicitacaoAtendimento.Nova};db.SolicitacoesAtendimento.Add(task);await db.SaveChangesAsync();
        check(await db.SolicitacoesAtendimento.AnyAsync(ConsultaPendencias.SolicitacoesEmAberto),"A02: tarefa telefônica vinculada à consulta aparece nas pendências");
        http.User=JornadaChecks.User(user.Id,"Funcionario");
        var taskAudit=new AuditoriaService(db,new HttpContextAccessor{HttpContext=http},NullLogger<AuditoriaService>.Instance);
        var taskController=new SolicitacoesController(db,new SolicitacaoAtendimentoService(db,taskAudit),svc.GetRequiredService<IClinicaClock>()){ControllerContext=new(){HttpContext=http},Url=new UrlHelper(action),TempData=new TempDataDictionary(http,new TempData())};
        await taskController.AssumirTarefa(task.Id,default);
        check(task.ResponsavelUsuarioId==user.Id&&task.Status==StatusSolicitacaoAtendimento.EmTriagem,"A02: assumir ligação registra o responsável");
        http.User=JornadaChecks.User("outro-atendente","Funcionario");
        check(await taskController.ConcluirTarefa(task.Id,default)is ForbidResult,"A02: outro atendente não conclui tarefa assumida por um colega");
        http.User=JornadaChecks.User(user.Id,"Funcionario");await taskController.ConcluirTarefa(task.Id,default);
        check(task.Status==StatusSolicitacaoAtendimento.Encerrada&&consultation.Status==ConsultaStatus.Pendente,"A02: concluir ligação encerra apenas a tarefa, sem confirmar ou duplicar consulta");
        db.SinaisTele.AddRange(new(){ConsultaId=consultation.Id,UsuarioId=user.Id,Protegido="old",CriadoEm=DateTime.UtcNow.AddHours(-1)},new(){ConsultaId=consultation.Id,UsuarioId=user.Id,Protegido="active",CriadoEm=DateTime.UtcNow});await db.SaveChangesAsync();
        check(await EphemeralCleanupBackgroundService.LimparAsync(db)==1&&await db.SinaisTele.CountAsync()==1,"A18: limpeza elimina sinal antigo e preserva sinalização ativa");
        var vm=new SolicitacoesPainelViewModel{Registros=[],PaginaConsulta=3,SituacaoConsulta="todas",ContagensConsulta=new Dictionary<string,int>{{"todas",53}}};
        check(vm.TotalPaginasConsulta==3&&vm.PaginaAtualConsulta==3&&vm.TotalConsultasFiltradas==53,"A11: totais e paginação usam agregações do servidor");
        await KeyRingChecks(check);
    }
    private static async Task KeyRingChecks(Action<bool,string> check)
    {
        var root=new InMemoryDatabaseRoot();var options=new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("keys-"+Guid.NewGuid(),root).Options;
        using var rsa=RSA.Create(3072);var request=new CertificateRequest("CN=CallMed-Audit-Only",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(1));
        ServiceProvider Provider(bool protect){var services=new ServiceCollection();services.AddLogging();services.AddScoped(_=>new MKSANContext(options));var dp=services.AddDataProtection().SetApplicationName("CallMed").PersistKeysToDbContext<MKSANContext>();if(protect)dp.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);return services.BuildServiceProvider();}
        string token;
        using(var first=Provider(false))
        {
            token=first.GetRequiredService<IDataProtectionProvider>().CreateProtector("audit").Protect("link anterior");
            using var scope=first.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MKSANContext>();
            var ids=await db.DataProtectionKeys.Select(k=>k.Id).ToArrayAsync();
            check(await KeyRingProtection.ProtegerLegadasAsync(db,certificate,NullLoggerFactory.Instance)>0,"A07: migração protege chaves legadas com certificado externo");
            check(await db.DataProtectionKeys.AllAsync(k=>!k.Xml!.Contains("<value>")&&k.Xml.Contains("encryptedSecret")),"A07: dump do key ring não contém segredo mestre em claro");
            var restoredIds=await db.DataProtectionKeys.Select(k=>k.Id).ToArrayAsync();
            check(ids.SequenceEqual(restoredIds),"A07: proteção preserva identificadores das chaves existentes");
            check(await KeyRingProtection.ProtegerLegadasAsync(db,certificate,NullLoggerFactory.Instance)==0,"A07: proteção de chaves é idempotente");
        }
        using(var restarted=Provider(true)) check(restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("audit").Unprotect(token)=="link anterior","A07: outro processo lógico restaura link anterior após proteção do key ring");
    }
    private sealed class AuditEmail : MKSANCrud.Services.Email.IEmailService
    {
        public bool Configurado=>true; public int Count; public string Html="";
        public Task<bool> EnviarAsync(string destinatario,string assunto,string corpoHtml,CancellationToken ct=default,string? replyTo=null){Count++;Html=corpoHtml;return Task.FromResult(true);}
    }
    private sealed class TempData : ITempDataProvider
    { public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){} }
}
