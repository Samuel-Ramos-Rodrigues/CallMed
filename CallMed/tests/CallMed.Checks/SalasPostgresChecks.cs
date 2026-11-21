using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Database;
internal static class SalasPostgresChecks {
 public static async Task RunAsync(Action<bool,string> check) {
  var connection=Environment.GetEnvironmentVariable("CALLMED_TEST_DB")??throw new Exception("Isolated database required");var cs=new Npgsql.NpgsqlConnectionStringBuilder(connection);
  if(cs.Host!="127.0.0.1"||!cs.Database!.StartsWith("callmed_test"))throw new Exception("Only isolated local test databases accepted");
  MKSANContext Db()=>new(new DbContextOptionsBuilder<MKSANContext>().UseNpgsql(connection).Options);var clock=new SalasChecks.Clock();var staff=JornadaChecks.User("f","Funcionario");
  await using(var db=Db()) {
   await db.Database.EnsureDeletedAsync();await db.Database.EnsureCreatedAsync();db.Salas.Add(new(){Id=1,Nome="Sala legada"});await db.SaveChangesAsync();
   await db.Database.ExecuteSqlRawAsync("DROP TABLE \"SessoesTele\"; DROP TABLE \"BloqueiosSala\"; ALTER TABLE \"Salas\" DROP COLUMN \"Localizacao\", DROP COLUMN \"Recursos\";");
   await new DatabaseSchemaV36Initializer(db).AplicarAsync();await new DatabaseSchemaV36Initializer(db).AplicarAsync();db.ChangeTracker.Clear();
   var s=await db.Salas.SingleAsync();check(s.Nome=="Sala legada"&&s.Localizacao==""&&s.Recursos=="","V36 é aditiva, reaplicável e preserva salas legadas no PostgreSQL");
   db.Users.AddRange(new Usuario{Id="f",UserName="f@ficticio.invalid"},new Usuario{Id="p",UserName="p@ficticio.invalid"},new Usuario{Id="m",UserName="m@ficticio.invalid"});
   db.Pacientes.Add(new(){Id=1,Nome="Paciente fictício",Cpf="52998224725",UsuarioId="p",DataNascimento=new(2000,1,1)});db.Especialidades.Add(new(){Id=1,Nome="Clínica geral"});
   db.Medicos.Add(new(){Id=1,Nome="Profissional fictício",UsuarioId="m",EspecialidadeId=1,AreaProfissional="Medicina",RegistroConselho="CRM",RegistroNumero="123",RegistroUf="SE",RegistroVerificadoEm=DateTime.UtcNow});
   db.Consultas.Add(new(){Id=1,PacienteId=1,MedicoId=1,Data=clock.Hoje,Horario="11:00",Status=ConsultaStatus.Confirmada});await db.SaveChangesAsync();
  }
  async Task<string?> Reservar(){await using var db=Db();return await new JornadaService(db,clock).AlocarAsync(await db.Consultas.SingleAsync(),staff,1,30,false,default);}
  async Task<string?> Bloquear(){await using var db=Db();var j=new JornadaService(db,clock);return await new SalasService(db,j,clock).BloquearAsync(1,clock.Hoje.AddHours(11),clock.Hoje.AddHours(12),"Manutenção",staff,default);}
  var race=await Task.WhenAll(Reservar(),Bloquear());check(race.Count(x=>x==null)==1,"Reserva e manutenção concorrentes não ocupam o mesmo intervalo");
  await using(var db=Db()){var j=await db.Jornadas.SingleOrDefaultAsync();var b=await db.BloqueiosSala.CountAsync(x=>x.CanceladoEm==null);check((j?.SalaId==1)!=(b==1),"Concorrência persiste resultado consistente");if(j==null){j=new(){ConsultaId=1};db.Jornadas.Add(j);}j.SalaId=null;j.Tele=true;j.ConsentimentoEm=DateTime.UtcNow;j.ConsentimentoVersao=ConsentimentoAtendimento.Versao(await db.Consultas.Include(c=>c.Medico).SingleAsync());await db.SaveChangesAsync();}
  async Task<Guid?> Entrar(){await using var db=Db();var j=new JornadaService(db,clock);return await new TeleconsultaPresencaService(db,j,new(db,j,clock),new SalasChecks.Time()).EntrarAsync(1,JornadaChecks.User("p","Paciente"),default);}
  var entradas=await Task.WhenAll(Entrar(),Entrar());check(entradas.All(x=>x.HasValue),"Entradas concorrentes são serializadas");
  await using(var db=Db()){check(await db.SessoesTele.CountAsync(x=>x.SaidaEm==null)==1&&await db.SessoesTele.CountAsync()==2,"Só a entrada mais recente mantém presença; histórico preservado");var s=await db.SessoesTele.SingleAsync(x=>x.SaidaEm==null);var j=new JornadaService(db,clock);var svc=new TeleconsultaPresencaService(db,j,new(db,j,clock),new SalasChecks.Time());check(await svc.AtualizarAsync(1,s.Id,"conectada",JornadaChecks.User("p","Paciente"),default),"Heartbeat funciona no PostgreSQL real");check(await svc.SairAsync(1,s.Id,JornadaChecks.User("p","Paciente"),default),"Saída encerra presença e mantém metadados");}
 }
}
