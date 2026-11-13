using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MKSANCrud.Application.Consultas;
using MKSANCrud.Application.Queries;
using MKSANCrud.Data;
using MKSANCrud.Extensions;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

internal static class ArchitectureChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        static ServiceCollection Services(string? mode)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:MKSANContextConnection"]="Host=127.0.0.1;Database=callmed_test_architecture",
                ["Jobs:Mode"]=mode, ["PublicOrigin"]="https://callmed.example.test"
            }).Build();
            var env = new TestEnvironment(); var services = new ServiceCollection();
            services.AddLogging(); services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<IWebHostEnvironment>(env);services.AddSingleton<IHostEnvironment>(env);
            services.AddCallMedApplication(config,env);
            return services;
        }
        static int Jobs(IServiceCollection services) => services.Count(s=>s.ServiceType==typeof(IHostedService)
            && s.ImplementationType?.Assembly==typeof(ConsultaWorkflow).Assembly);
        var services=Services(null);
        check(Jobs(services)==10,"V35: modo padrão mantém as dez rotinas, incluindo httpSMS");
        check(Jobs(Services("external"))==0,"V35: modo externo remove as rotinas do processo web");
        check(Jobs(Services("disabled"))==0,"V35: desativação explícita não inicia rotinas");
        bool invalid=false;try{Services("typo");}catch(InvalidOperationException){invalid=true;}
        check(invalid,"V35: modo de execução inválido é recusado");
        services.RemoveAll<DbContextOptions<MKSANContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<MKSANContext>>();
        services.AddDbContext<MKSANContext>(o=>o.UseInMemoryDatabase("architecture-"+Guid.NewGuid()).ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddSingleton<IClinicaClock>(new SalasChecks.Clock());
        await using var sp=services.BuildServiceProvider();using var scope=sp.CreateScope();var svc=scope.ServiceProvider;
        var db=svc.GetRequiredService<MKSANContext>();var clock=svc.GetRequiredService<IClinicaClock>();
        db.Users.AddRange(new Usuario{Id="p1",UserName="p1@example.test"},new Usuario{Id="p2",UserName="p2@example.test"});
        db.Pacientes.AddRange(new(){Id=1,Nome="Primeiro paciente",Cpf="52998224725",UsuarioId="p1",Ativo=true},new(){Id=2,Nome="Segundo paciente",Cpf="11144477735",UsuarioId="p2",Ativo=true});
        db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});
        db.Profissionais.Add(new(){Id=1,Nome="Profissional fictício",EspecialidadeId=1,AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="123",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow});
        db.Consultas.AddRange(new(){Id=1,PacienteId=1,MedicoId=1,Data=clock.Hoje.AddDays(2),Horario="10:00",Status=ConsultaStatus.Pendente},new(){Id=2,PacienteId=2,MedicoId=1,Data=clock.Hoje.AddDays(2),Horario="11:00",Status=ConsultaStatus.Pendente});
        await db.SaveChangesAsync();
        var workflow=svc.GetRequiredService<ConsultaWorkflow>();
        static ClaimsPrincipal User(string id,string role)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id),new Claim(ClaimTypes.Role,role)},"test"));
        var p=User("p1","Paciente");
        check(!(await workflow.ExecuteAsync(2,"cancelar",p,"teste")).Sucesso,"V35: paciente não cancela consulta de outra pessoa");
        check((await db.Consultas.FindAsync(2))!.Status==ConsultaStatus.Pendente,"V35: tentativa sem permissão preserva a consulta");
        check(!(await workflow.ExecuteAsync(1,"realizar",p,"teste")).Sucesso,"V35: paciente não conclui atendimento da equipe");
        check(!(await workflow.ExecuteAsync(1,"confirmar",User("p1","Outro"),"teste")).Sucesso,"V35: vínculo sem papel de paciente não autoriza mutação");
        check((await workflow.ExecuteAsync(1,"confirmar",p,"teste")).Sucesso,"V35: confirmação própria usa o caso de uso compartilhado");
        var count=await db.AuditoriaEventos.CountAsync();
        check((await workflow.ExecuteAsync(1,"confirmar",p,"teste")).Sucesso&&await db.AuditoriaEventos.CountAsync()==count,"V35: repetição de confirmação não duplica auditoria");
        check((await workflow.ExecuteAsync(1,"cancelar",p,"teste")).Sucesso,"V35: cancelamento próprio mantém a mesma regra no site e Android");
        var list=svc.GetRequiredService<ConsultaListQuery>();
        var own=await list.ExecuteAsync(p,null,null);
        check(own.Paciente?.All(c=>c.PacienteId==1)==true&&own.Equipe is null,"V35: leitura do paciente permanece isolada");
        check((await list.ExecuteAsync(User("p1","Outro"),null,null)).AcessoNegado,"V35: consulta compartilhada recusa perfil desconhecido");
        check((await list.ExecuteAsync(new ClaimsPrincipal(new ClaimsIdentity()),null,null)).AcessoNegado,"V35: consulta compartilhada exige autenticação");
    }
    private sealed class TestEnvironment:IWebHostEnvironment
    {
        public string ApplicationName{get;set;}="CallMedCrud";
        public string EnvironmentName{get;set;}=Environments.Development;
        public string ContentRootPath{get;set;}=Path.GetFullPath("CallMedCrud");
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
        public string WebRootPath{get;set;}=Path.GetFullPath("CallMedCrud/wwwroot");
        public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
