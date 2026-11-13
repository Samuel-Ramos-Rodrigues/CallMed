using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
internal static class SalasChecks {
 public static async Task RunAsync(Action<bool,string> check) {
  await using var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("salas-"+Guid.NewGuid()).Options);
  var clock=new Clock();var time=new Time();var j=new JornadaService(db,clock);var salas=new SalasService(db,j,clock);var acesso=new TeleconsultaAcessoService(db,j,clock);var presenca=new TeleconsultaPresencaService(db,j,acesso,time);
  var staff=JornadaChecks.User("f","Funcionario");var patient=JornadaChecks.User("p","Paciente");var doctor=JornadaChecks.User("m","Medico");
  db.Pacientes.Add(new(){Id=1,UsuarioId="p",Nome="Paciente fictício",Cpf="52998224725",DataNascimento=new(2000,1,1)});db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});
  db.Medicos.Add(new(){Id=1,UsuarioId="m",Nome="Profissional fictício",EspecialidadeId=1,AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="123",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow});
  db.Consultas.Add(new(){Id=1,PacienteId=1,MedicoId=1,Data=clock.Hoje,Horario="11:00",Status=ConsultaStatus.Confirmada});await db.SaveChangesAsync();
  check(await salas.SalvarAsync(null,"Consultório 1","Térreo","Acesso sem degraus",true,staff,default)==null,"Equipe cadastra sala com localização e recursos");var sala=await db.Salas.SingleAsync();
  check(sala.Localizacao=="Térreo"&&sala.Recursos=="Acesso sem degraus","Informações do espaço são preservadas");
  check(await salas.SalvarAsync(null," consultório 1 ",null,null,true,staff,default)!=null,"Nomes duplicados de salas são recusados");
  check(await salas.SalvarAsync(null,"Outra",null,null,true,patient,default)!=null,"Paciente não cadastra sala");
  check(await salas.SalvarAsync(null,"Outra",null,null,true,doctor,default)!=null,"Profissional não altera recursos administrativos");
  check(await salas.BloquearAsync(sala.Id,clock.Hoje.AddHours(11),clock.Hoje.AddHours(12),"Manutenção",staff,default)==null,"Manutenção bloqueia o intervalo");
  check(await salas.BloquearAsync(sala.Id,clock.Hoje.AddHours(11.5),clock.Hoje.AddHours(12.5),"Outro",staff,default)!=null,"Bloqueios sobrepostos são recusados");
  var c=await db.Consultas.Include(c=>c.Medico).Include(c=>c.Paciente).SingleAsync();
  check(await j.AlocarAsync(c,staff,sala.Id,30,false,default)!=null,"Consulta não ocupa sala bloqueada");
  check(await salas.LiberarAsync((await db.BloqueiosSala.SingleAsync()).Id,staff,default)==null,"Liberação preserva histórico do bloqueio");
  check(await j.AlocarAsync(c,staff,sala.Id,30,false,default)==null,"Consulta reserva sala após liberação");
  check(await salas.BloquearAsync(sala.Id,clock.Hoje.AddHours(11.25),clock.Hoje.AddHours(12),"Manutenção",staff,default)!=null,"Manutenção não atropela uma consulta reservada");
  check(await salas.SalvarAsync(sala.Id,sala.Nome,sala.Localizacao,sala.Recursos,false,staff,default)!=null,"Sala com reservas futuras não pode ser desativada");
  check(await salas.BloquearAsync(sala.Id,clock.Hoje.AddHours(11.5),clock.Hoje.AddHours(12),"Intervalo adjacente",staff,default)==null,"Intervalos adjacentes são permitidos");
  check(await j.AlocarAsync(c,staff,null,30,true,default)==null,"Modalidade online libera a sala física");
  check(await salas.SalvarAsync(sala.Id,sala.Nome,sala.Localizacao,sala.Recursos,false,staff,default)==null,"Sala sem reservas pode ser desativada");
  check(await j.AlocarAsync(c,staff,sala.Id,30,false,default)!=null,"Sala inativa rejeita nova reserva");
  check(await presenca.EntrarAsync(1,patient,default)==null,"Presença exige consentimento");
  check(await j.MoverAsync(c,patient,"consentir",true,default)==null,"Paciente autoriza a consulta online");
  var session=await presenca.EntrarAsync(1,patient,default);check(session.HasValue,"Paciente autorizado registra entrada");
  check(await presenca.EntrarAsync(1,staff,default)==null,"Recepção não participa do vídeo");
  check(await presenca.EntrarAsync(1,JornadaChecks.User("outro","Paciente"),default)==null,"Outro paciente não registra presença");
  check(!await presenca.AtualizarAsync(1,session!.Value,"conectada",doctor,default),"Participante não altera presença alheia");
  check(!await presenca.AtualizarAsync(2,session.Value,"conectada",patient,default),"Sessão não pode ser usada em outra consulta");
  check(!await presenca.AtualizarAsync(1,session.Value,"inventado",patient,default),"Estado desconhecido é recusado");
  check(await presenca.AtualizarAsync(1,session.Value,"conectada",patient,default),"Dispositivo autorizado atualiza conexão");
  var atual=await db.SessoesTele.SingleAsync(x=>x.Id==session);check(atual.ConectadaEm.HasValue&&TeleconsultaPresencaService.Presente(atual,c,time.GetUtcNow().UtcDateTime),"Conexão e presença são registradas");
  time.Utc=time.Utc.AddSeconds(61);check(!TeleconsultaPresencaService.Presente(atual,c,time.GetUtcNow().UtcDateTime),"Presença expira após 60 segundos");
  check(!await presenca.AtualizarAsync(1,session.Value,"conectada",patient,default),"Heartbeat atrasado não ressuscita presença");
  var nova=await presenca.EntrarAsync(1,patient,default);check(nova.HasValue&&atual.SaidaEm.HasValue,"Nova entrada encerra sessão anterior");
  check(!await presenca.SairAsync(1,nova!.Value,doctor,default),"Profissional não encerra presença do paciente");
  c.Revisao++;await db.SaveChangesAsync();check(!await presenca.AtualizarAsync(1,nova.Value,"conectada",patient,default),"Alteração de agendamento invalida presença antiga");
  check(await presenca.SairAsync(1,nova.Value,patient,default),"Saída é aceita após alteração da consulta");
  check(await presenca.SairAsync(1,nova.Value,patient,default),"Saída repetida é idempotente");
  c.Status=ConsultaStatus.Cancelada;await db.SaveChangesAsync();check(await presenca.EntrarAsync(1,patient,default)==null,"Consulta cancelada não admite presença");
  await salas.SalvarAsync(sala.Id,sala.Nome,sala.Localizacao,sala.Recursos,true,staff,default);
  db.Pacientes.Add(new(){Id=2,Nome="Outro fictício",Cpf="11144477735",DataNascimento=new(2000,1,1)});db.Medicos.Add(new(){Id=2,Nome="Outro profissional",EspecialidadeId=1});
  var noite=new Consulta{Id=100,PacienteId=1,MedicoId=1,Data=clock.Hoje,Horario="23:45",Status=ConsultaStatus.Confirmada};var madrugada=new Consulta{Id=101,PacienteId=2,MedicoId=2,Data=clock.Hoje.AddDays(1),Horario="00:00",Status=ConsultaStatus.Confirmada};db.Consultas.AddRange(noite,madrugada);await db.SaveChangesAsync();
  check(await j.AlocarAsync(noite,staff,sala.Id,30,false,default)==null,"Reserva física pode atravessar a meia-noite");
  check(await j.AlocarAsync(madrugada,staff,sala.Id,30,false,default)!=null,"Sala não aceita conflito com reserva iniciada no dia anterior");
  madrugada.Horario="00:15";await db.SaveChangesAsync();check(await j.AlocarAsync(madrugada,staff,sala.Id,30,false,default)==null,"Sala aceita horário adjacente após a meia-noite");
 }
 internal sealed class Clock:IClinicaClock {public DateTime Agora{get;set;}=new(2026,11,10,10,45,0);public DateTime Hoje=>Agora.Date;public DateTime ConverterUtc(DateTime utc)=>DateTime.SpecifyKind(utc,DateTimeKind.Unspecified).AddHours(-3);public DateTime ConverterParaUtc(DateTime local)=>DateTime.SpecifyKind(local.AddHours(3),DateTimeKind.Utc);}
 internal sealed class Time:TimeProvider {public DateTimeOffset Utc=new(2026,11,10,13,45,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Utc;}
}
