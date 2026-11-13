using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

internal static class TeleconsultaChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var clock=new Clock();
        await using var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("tele-"+Guid.NewGuid()).ConfigureWarnings(w=>w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var p=new Paciente{Id=1,UsuarioId="p",Nome="Paciente fictício",Cpf="52998224725",DataNascimento=new(2000,1,1)};
        var m=new Medico{Crp="12/12345",RegistroVerificadoEm=DateTime.UtcNow,Id=1,UsuarioId="m",Nome="Profissional fictício",EspecialidadeId=1};
        db.Pacientes.Add(p);db.Medicos.Add(m);db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});
        var c=new Consulta{Id=1,Paciente=p,Medico=m,Data=clock.Hoje,Horario="11:00",Status=ConsultaStatus.Confirmada};
        db.Consultas.Add(c);var j=new Jornada{ConsultaId=1};db.Jornadas.Add(j);await db.SaveChangesAsync();
        var flow=new JornadaService(db,clock);var service=new TeleconsultaAcessoService(db,flow,clock);
        var patient=JornadaChecks.User("p","Paciente");var doctor=JornadaChecks.User("m","Medico");var staff=JornadaChecks.User("f","Funcionario");
        check(await service.ObterAsync(1,JornadaChecks.User("outro","Paciente"),default)==null,"Sala não revela consulta de outro paciente");
        check(await service.ObterAsync(1,JornadaChecks.User("outro","Medico"),default)==null,"Outro profissional não acessa sala alheia");
        check((await service.ObterAsync(1,patient,default))?.Estado=="presencial","Consulta presencial mostra orientação sem liberar mídia");
        j.Tele=true;await db.SaveChangesAsync();
        var s=await service.ObterAsync(1,patient,default);
        check(s?.Estado=="consentimento"&&s.PodeConsentir&&!s.PodeEntrar,"Paciente recebe próximo passo para autorizar vídeo");
        s=await service.ObterAsync(1,doctor,default);
        check(s?.Estado=="consentimento"&&!s.PodeConsentir&&!s.PodeEntrar,"Profissional aguarda consentimento e não aceita pelo paciente");
        s=await service.ObterAsync(1,staff,default);
        check(s?.Estado=="equipe"&&!s.PodeEntrar&&!s.PodeConsentir,"Recepção acompanha orientações sem acessar mídia ou consentir");
        check((await service.ObterAsync(1,JornadaChecks.User("a","Admin"),default))?.Estado=="equipe","Admin acompanha a consulta sem participar do vídeo");
        j.ConsentimentoEm=DateTime.UtcNow;j.ConsentimentoVersao=MKSANCrud.Services.Psicologia.ConsentimentoPsicologia.Versao;await db.SaveChangesAsync();
        check((await service.ObterAsync(1,patient,default))?.Estado=="aguardando-horario","Antes da abertura aparece horário e espera, sem link de entrada ativo");
        clock.Agora=clock.Hoje.AddHours(10).AddMinutes(30);
        check((await service.ObterAsync(1,patient,default))?.PodeEntrar==true,"Sala libera exatamente 30 minutos antes");
        clock.Agora=clock.Hoje.AddHours(15);
        check((await service.ObterAsync(1,doctor,default))?.PodeEntrar==true,"Sala continua liberada até o limite de quatro horas");
        clock.Agora=clock.Agora.AddTicks(1);
        check((await service.ObterAsync(1,patient,default))?.Estado=="prazo-encerrado","Após o prazo, apresenta expiração sem liberar sinalização");
        c.Status=ConsultaStatus.Cancelada;await db.SaveChangesAsync();
        check((await service.ObterAsync(1,patient,default))?.Estado=="encerrada","Consulta cancelada tem orientação própria");
        c.Status=ConsultaStatus.Confirmada;c.Horario="inválido";await db.SaveChangesAsync();
        check((await service.ObterAsync(1,patient,default))?.Estado=="horario-invalido","Horário inválido não causa exceção nem abre sala");
        check(await flow.MoverAsync(c,doctor,"iniciar",true,default)!=null,"Início online também recusa horário inválido sem exceção");
        c.Horario="00:15";c.Data=new DateTime(2026,11,8);clock.Agora=new DateTime(2026,11,7,23,50,0);await db.SaveChangesAsync();
        check((await service.ObterAsync(1,patient,default))?.PodeEntrar==true,"Janela online funciona ao atravessar a meia-noite");
        check(await flow.MoverAsync(c,doctor,"iniciar",false,default)!=null&&j.ChegadaEm==null,"Profissional confirma identidade explicitamente antes de iniciar online");
        j.ConsentimentoEm=null;await db.SaveChangesAsync();
        check(await flow.MoverAsync(c,doctor,"iniciar",true,default)!=null,"Início online exige autorização do paciente");
        j.ConsentimentoEm=DateTime.UtcNow;j.ConsentimentoVersao=MKSANCrud.Services.Psicologia.ConsentimentoPsicologia.Versao;await db.SaveChangesAsync();clock.Agora=new DateTime(2026,11,7,20,0,0);
        check(await flow.MoverAsync(c,doctor,"iniciar",true,default)!=null,"Início online respeita o período de acesso à sala");
        clock.Agora=new DateTime(2026,11,7,23,50,0);
        check(await flow.MoverAsync(c,doctor,"iniciar",true,default)==null&&j.InicioEm!=null&&j.ChegadaEm!=null,"Profissional inicia online após validar identidade, sem chegada presencial");
        check(await flow.MoverAsync(c,doctor,"concluir",true,default)==null,"Conclusão online mantém atualização da agenda");
        check(!(await service.ObterAsync(1,patient,default))!.PodeEntrar,"Conclusão revoga o acesso à sala");
    }
    sealed class Clock:IClinicaClock
    {
        public DateTime Agora{get;set;}=new(2026,11,7,10,0,0);
        public DateTime Hoje=>Agora.Date;
        public DateTime ConverterUtc(DateTime utc)=>DateTime.SpecifyKind(utc,DateTimeKind.Unspecified).AddHours(-3);
        public DateTime ConverterParaUtc(DateTime local)=>DateTime.SpecifyKind(local.AddHours(3),DateTimeKind.Utc);
    }
}
