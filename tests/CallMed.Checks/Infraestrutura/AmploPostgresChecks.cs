using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Database;
internal static class AmploPostgresChecks {
 public static async Task RunAsync(Action<bool,string> check) {
  var connection=Environment.GetEnvironmentVariable("CALLMED_TEST_DB")??throw new Exception("Local isolated DB required");var cs=new Npgsql.NpgsqlConnectionStringBuilder(connection);if(cs.Host!="127.0.0.1"||!cs.Database!.StartsWith("callmed_test"))throw new Exception("Local isolated DB required");
  await using var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseNpgsql(connection).Options);await db.Database.EnsureDeletedAsync();await db.Database.EnsureCreatedAsync();
  db.Especialidades.Add(new(){Id=1,Nome="Clínica"});db.Profissionais.AddRange(new(){Id=1,Nome="Legado CRM",Crm="CRM-PRESERVADO",EspecialidadeId=1},new(){Id=2,Nome="Legado CRP",Crp="12/12345",RegistroVerificadoEm=DateTime.UtcNow,EspecialidadeId=1});await db.SaveChangesAsync();
  foreach(var col in new[]{"AreaProfissional","RegistroConselho","RegistroNumero","RegistroUf"})await PgTestDdl.ExecuteAsync(db,"ALTER TABLE \"Medicos\" DROP COLUMN \"",col,"\" CASCADE");db.ChangeTracker.Clear();
  await new DatabaseSchemaV35Initializer(db).AplicarAsync();await new DatabaseSchemaV35Initializer(db).AplicarAsync();
  var legado=await db.Profissionais.SingleAsync(x=>x.Id==1);var psi=await db.Profissionais.SingleAsync(x=>x.Id==2);
  check(legado.Crm=="CRM-PRESERVADO"&&legado.Crp==null&&!legado.HabilitadoClinicamente,"V35 reaplicável preserva CRM legado sem conversão ou habilitação implícita");
  check(psi.EhPsicologia&&psi.HabilitadoPsicologia,"V35 preserva a habilitação de CRP previamente verificado");
  db.Profissionais.AddRange(new(){Id=3,Nome="Médico SE",AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="12345",RegistroUf="SE",EspecialidadeId=1},new(){Id=4,Nome="Médico BA",AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="12345",RegistroUf="BA",EspecialidadeId=1});await db.SaveChangesAsync();check(await db.Profissionais.CountAsync()==4,"Registros de mesmo número em UFs distintas não colidem");
  db.Profissionais.Add(new(){Id=5,Nome="Duplicado",AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="12345",RegistroUf="SE",EspecialidadeId=1});var blocked=false;try{await db.SaveChangesAsync();}catch(DbUpdateException e)when(e.InnerException is Npgsql.PostgresException{SqlState:"23505"}){blocked=true;}check(blocked,"Banco impede registro duplicado no mesmo conselho e UF");
  db.ChangeTracker.Clear();var profissional=await db.Profissionais.SingleAsync(x=>x.Id==3);profissional.DuracaoPadraoMinutos=15;profissional.RegistroVerificadoEm=DateTime.UtcNow;
  var clock=new MKSANCrud.Services.Clinica.ClinicaClock();var dia=clock.Hoje.AddDays(1);db.Pacientes.AddRange(new Paciente{Id=1,Nome="Fictício A",Cpf="52998224725"},new Paciente{Id=2,Nome="Fictício B",Cpf="11144477735"});db.Disponibilidades.AddRange(new(){MedicoId=3,Data=dia,Horario="10:00",Ativo=true},new(){MedicoId=3,Data=dia,Horario="10:15",Ativo=true});await db.SaveChangesAsync();
  var convenio=new MKSANCrud.Services.Clinica.ConvenioService(clock);var agenda=new MKSANCrud.Services.Agendamento.AgendamentoService(db,clock,new(db),convenio,new(db,convenio),Microsoft.Extensions.Logging.Abstractions.NullLogger<MKSANCrud.Services.Agendamento.AgendamentoService>.Instance);
  var r1=await agenda.AgendarAsync(1,3,dia,"10:00",null);var r2=await agenda.AgendarAsync(2,3,dia,"10:15",null);check(r1.Sucesso&&r2.Sucesso&&r1.Consulta!.DuracaoMinutos==15,"Agenda médica respeita duração de 15 minutos sem impor o mínimo anterior de Psicologia");

 }
}
