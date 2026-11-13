using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Database;
internal static class PilotoPostgresChecks {
 public static async Task RunAsync(Action<bool,string> check) {
  var connection=Environment.GetEnvironmentVariable("CALLMED_TEST_DB")??throw new Exception("Isolated local database required");
  var cs=new Npgsql.NpgsqlConnectionStringBuilder(connection);
  if(cs.Host!="127.0.0.1"||!cs.Database!.StartsWith("callmed_test"))throw new Exception("Only local isolated test databases are accepted");
  MKSANContext Db()=>new(new DbContextOptionsBuilder<MKSANContext>().UseNpgsql(connection).Options);
  var clock=new PilotoChecks.Clock();var dia=clock.Hoje.AddDays(-2);
  await using(var db=Db()) {
   await db.Database.EnsureDeletedAsync();await db.Database.EnsureCreatedAsync();
   db.MedicoesOperacionais.Add(new(){Data=dia,Periodo="Antes",Consultas=10,Faltas=2,MinutosRecepcao=30,Fonte="Fonte antiga preservada"});
   db.Especialidades.Add(new(){Id=1,Nome="Psicologia"});db.Psicologos.Add(new(){Id=1,Nome="Fictício",EspecialidadeId=1});db.Pacientes.Add(new(){Id=1,Nome="Fictício",Cpf="52998224725"});
   db.Consultas.Add(new(){Id=1,MedicoId=1,PacienteId=1,Data=dia,Horario="10:00",Status=ConsultaStatus.Realizada,DuracaoMinutos=50});
   db.Jornadas.Add(new(){ConsultaId=1,Duracao=40});await db.SaveChangesAsync();
   await db.Database.ExecuteSqlRawAsync("DROP TABLE \"MedicoesPiloto\"");
   await new DatabaseSchemaV34Initializer(db).AplicarAsync();await new DatabaseSchemaV34Initializer(db).AplicarAsync();
   check((await db.MedicoesOperacionais.SingleAsync()).Fonte=="Fonte antiga preservada"&&(await db.Consultas.SingleAsync()).DuracaoMinutos==50,"V34 reaplicável preserva consultas e medições legadas no PostgreSQL");
   var svc=new PilotoService(db,clock);var agenda=await svc.AgendaAsync(dia);check(agenda.Realizadas==1&&agenda.MinutosAgendados==40,"Fotografia da agenda é traduzida e calculada no PostgreSQL");
   var input=PilotoChecks.Input(dia,"Piloto");input.AgendaHash=agenda.Hash;var row=await svc.RegistrarAsync(input,"operador-ficticio");
   check(row.CustoHoraRecepcao==30.50m&&await db.MedicoesPiloto.CountAsync()==1,"Nova medição persiste precisão monetária, autoria e fotografia");
   foreach(var sql in new[]{"UPDATE \"MedicoesPiloto\" SET \"Fonte\"='alterada'","DELETE FROM \"MedicoesPiloto\""}) {
    var blocked=false;try{await db.Database.ExecuteSqlRawAsync(sql);}catch(Npgsql.PostgresException){blocked=true;}check(blocked,"PostgreSQL impede alteração ou exclusão direta de revisão publicada");
   }
  }
  async Task<bool> Salvar() {
   await using var db=Db();var svc=new PilotoService(db,clock);var input=PilotoChecks.Input(dia,"Piloto",1);input.AgendaHash=(await svc.AgendaAsync(dia)).Hash;
   try{await svc.RegistrarAsync(input,"concorrente-ficticio");return true;}catch(MedicaoConflitoException){return false;}
  }
  var corrida=await Task.WhenAll(Salvar(),Salvar());check(corrida.Count(x=>x)==1,"Duas revisões simultâneas produzem um único vencedor sem sobrescrita");
  await using(var db=Db()) {
   var svc=new PilotoService(db,clock);var rows=await db.MedicoesPiloto.AsNoTracking().ToListAsync();check(rows.Count==2&&rows.Max(x=>x.Revisao)==2,"Concorrência preserva duas revisões sequenciais no banco");
   var before=PilotoChecks.Input(dia.AddDays(-1));await svc.RegistrarAsync(before,"ficticio");
   var rel=await svc.RelatorioAsync(dia.AddDays(-1),dia.AddDays(-1),dia,dia);
   check(rel.Comparacao.Piloto.DiasMedidos==1&&rel.Comparacao.Piloto.Realizadas==1&&rel.Medicoes.Length==2,"Relatório usa só revisões atuais e as mesmas fontes do cálculo");
   var retirada=PilotoChecks.Input(dia,"Piloto",2);retirada.Desconsiderar=true;retirada.Motivo="Data registrada incorretamente";await svc.RegistrarAsync(retirada,"ficticio");
   var sem=await svc.RelatorioAsync(dia.AddDays(-1),dia.AddDays(-1),dia,dia);
   check(sem.Comparacao.Piloto.DiasMedidos==0&&sem.Medicoes.Length==1&&await db.MedicoesPiloto.CountAsync(x=>x.Desconsiderada)==1,"Desconsideração persistida retira dia do agregado sem apagar evidência no PostgreSQL");
   check(rel.Comparacao.Antes.TaxaFaltas==20&&rel.Comparacao.Piloto.TaxaFaltas==0&&rel.Comparacao.DiferencaFaltasPp==-20,"Comparação calcula diferença em pontos percentuais com denominadores explícitos");
  }
 }
}
