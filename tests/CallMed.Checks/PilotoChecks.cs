using MKSANCrud.Application.Binding;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
internal static class PilotoChecks {
 internal sealed class Clock:IClinicaClock {
  public DateTime Hoje=>new(2026,11,9);public DateTime Agora=>Hoje.AddHours(12);
  public DateTime ConverterUtc(DateTime utc)=>DateTime.SpecifyKind(utc.AddHours(-3),DateTimeKind.Unspecified);
  public DateTime ConverterParaUtc(DateTime local)=>DateTime.SpecifyKind(local.AddHours(3),DateTimeKind.Utc);
 }
 internal static MedicaoPilotoInput Input(DateTime dia,string etapa="Antes",int revisao=0)=>new(){Data=dia,Periodo=etapa,RevisaoAnterior=revisao,
  Realizadas=8,Faltas=2,Canceladas=1,SemDesfecho=0,MinutosAgendados=500,MinutosDisponiveis=600,
  MinutosRecepcao=30,CustoHoraRecepcao=30.50m,Fonte="Escala e cronometragem fictícias",Motivo="Conferência do dia",Conferido=true};
 public static async Task RunAsync(Action<bool,string> check) {
  var clock=new Clock();var dia=clock.Hoje.AddDays(-2);
  await using var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("piloto-"+Guid.NewGuid()).Options);var svc=new PilotoService(db,clock);
  var e=await svc.CompararAsync(dia.AddDays(-1),dia.AddDays(-1),dia,dia);
  check(e.Antes.TaxaFaltas==null&&e.Antes.Ocupacao==null&&e.Piloto.CustoRecepcao==null&&e.DiferencaFaltasPp==null,"Sem medições não há taxas, custos ou melhoria inventados");
  check(e.Piloto.DiasEsperados==1&&e.Piloto.DiasMedidos==0,"Dia sem medição permanece ausente, sem virar zero");
  var old=new MedicaoPiloto{Data=dia,Periodo="Antes",Revisao=1,Realizadas=100,Faltas=100,MinutosRecepcao=900};
  var r1=new MedicaoPiloto{Data=dia,Periodo="Antes",Revisao=2,Realizadas=8,Faltas=2,MinutosAgendados=100,MinutosDisponiveis=200,MinutosRecepcao=20,CustoHoraRecepcao=30};
  var r2=new MedicaoPiloto{Data=dia.AddDays(1),Periodo="Antes",Revisao=1,Realizadas=90,Faltas=10,MinutosAgendados=600,MinutosDisponiveis=800,MinutosRecepcao=100,CustoHoraRecepcao=60};
  var a=PilotoService.Resumir([old,r1,r2],"Antes",dia,dia.AddDays(1));
  check(a.Realizadas==98&&a.Faltas==12&&a.DiasMedidos==2,"Revisão substitui só o agregado, sem duplicar o dia");
  check(a.TaxaFaltas==10.9&&a.Ocupacao==70,"Taxas ponderam os denominadores reais, não as médias diárias");
  check(a.MinutosPorConsulta==1.09m&&a.CustoRecepcao==110m&&a.CustoPorConsulta==1m,"Trabalho e custo por consulta usam totais do mesmo conjunto de dias");
  r2.CustoHoraRecepcao=null;r2.MinutosDisponiveis=null;
  a=PilotoService.Resumir([r1,r2],"Antes",dia,dia.AddDays(2));
  check(a.DiasEsperados==3&&a.DiasOcupacao==1&&a.Ocupacao==50&&a.MinutosDisponiveis==200,"Capacidade desconhecida exclui somente a amostra de ocupação e explicita cobertura");
  check(a.DiasCusto==1&&a.ConsultasCusto==10&&a.CustoPorConsulta==1&&a.DiasRecepcao==2,"Custo ausente não contamina a média dos dias medidos nem vira custo zero");
  r1.MinutosAgendados=300;check(PilotoService.Resumir([r1],"Antes",dia,dia).Ocupacao==150,"Excesso de agendamento não é limitado artificialmente a 100%");
  r1.Realizadas=0;r1.Faltas=0;r1.Canceladas=1;check(PilotoService.Resumir([r1],"Antes",dia,dia).TaxaFaltas==null,"Dia só com cancelamentos não tem denominador de faltas");
  db.MedicoesOperacionais.Add(new(){Data=dia.AddDays(-1),Periodo="Antes",Consultas=100,Faltas=50,Fonte="Legado"});await db.SaveChangesAsync();
  e=await svc.CompararAsync(dia.AddDays(-1),dia.AddDays(-1),dia,dia);check(e.Antes.DiasMedidos==0,"Medição legada com consultas previstas não é convertida para a nova metodologia");
  foreach(var (d,p) in new[]{(clock.Hoje,"Antes"),(clock.Hoje.AddDays(1),"Piloto"),(dia,"Fora"),(new DateTime(1900,1,1),"Antes")}){var ok=false;try{svc.ValidarDia(d,p);}catch(ArgumentException){ok=true;}check(ok,"Dia aberto, futuro ou etapa inválida é recusado");}
  foreach(var dates in new[]{(dia,dia,dia,dia),(dia,dia.AddDays(1),dia,dia),(dia,dia.AddDays(-1),dia.AddDays(1),dia.AddDays(1)),(dia.AddDays(-400),dia,dia.AddDays(1),dia.AddDays(1))}){
   var ok=false;try{await svc.CompararAsync(dates.Item1,dates.Item2,dates.Item3,dates.Item4);}catch(ArgumentException){ok=true;}check(ok,"Comparação recusa sobreposição, ordem ou período excessivo");
  }
  foreach(var alter in new Action<MedicaoPilotoInput>[] {x=>x.Conferido=false,x=>x.Fonte=" ",x=>x.Motivo="",x=>x.Realizadas=null,x=>x.Faltas=-1,x=>x.MinutosRecepcao=-1,x=>x.CustoHoraRecepcao=30.501m,x=>{x.MinutosDisponiveis=0;x.MinutosAgendados=30;}}) {
   var input=Input(dia);alter(input);var ok=false;try{await svc.RegistrarAsync(input,"ficticio");}catch(ArgumentException){ok=true;}check(ok,"Valores inválidos, fonte ausente ou ausência de conferência não produzem medição");
  }
  var row=await svc.RegistrarAsync(Input(dia),"ficticio");check(row.Revisao==1&&row.CustoHoraRecepcao==30.50m,"Primeira medição mantém valor monetário e autoria");
  var conflito=false;try{await svc.RegistrarAsync(Input(dia),"outro");}catch(MedicaoConflitoException){conflito=true;}check(conflito,"Formulário desatualizado não substitui medição de outro operador");
  var revisao=Input(dia,revisao:1);revisao.Motivo="Correção de contagem";revisao.Faltas=1;await svc.RegistrarAsync(revisao,"outro");
  check(await db.MedicoesPiloto.CountAsync()==2&&(await svc.UltimaAsync(dia,"Antes"))!.Faltas==1,"Correção mantém a revisão anterior e elege somente a mais recente");
  row.Fonte="alterada";var immutable=false;try{await db.SaveChangesAsync();}catch(InvalidOperationException){immutable=true;}check(immutable,"EF impede edição de revisão publicada");db.Entry(row).State=EntityState.Unchanged;
  db.Consultas.AddRange(new Consulta{Id=1,Data=dia,Status=ConsultaStatus.Realizada,DuracaoMinutos=50},new Consulta{Id=2,Data=dia,Status=ConsultaStatus.Ausente,DuracaoMinutos=45},new Consulta{Id=3,Data=dia,Status=ConsultaStatus.Cancelada,DuracaoMinutos=40},new Consulta{Id=4,Data=dia,Status=ConsultaStatus.Pendente,DuracaoMinutos=20},new Consulta{Id=5,Data=dia.AddDays(-1),Status=ConsultaStatus.Realizada,DuracaoMinutos=50});
  db.Jornadas.Add(new Jornada{ConsultaId=1,Duracao=30});await db.SaveChangesAsync();
  var agenda=await svc.AgendaAsync(dia);check(agenda.Realizadas==1&&agenda.Faltas==1&&agenda.Canceladas==1&&agenda.SemDesfecho==1&&agenda.MinutosAgendados==95,"Fotografia da agenda usa desfechos do dia e duração da jornada, excluindo canceladas dos minutos");
  var pInput=Input(dia,"Piloto");pInput.AgendaHash=agenda.Hash;pInput.Realizadas=999;pInput.MinutosAgendados=999;
  var registroPiloto=await svc.RegistrarAsync(pInput,"ficticio");check(registroPiloto.Realizadas==1&&registroPiloto.MinutosAgendados==95&&registroPiloto.AgendaHash==agenda.Hash,"Totais do piloto são recalculados no servidor, sem confiar no formulário");
  var c=await db.Consultas.SingleAsync(x=>x.Id==4);c.Status=ConsultaStatus.Cancelada;await db.SaveChangesAsync();pInput.RevisaoAnterior=1;
  conflito=false;try{await svc.RegistrarAsync(pInput,"ficticio");}catch(MedicaoConflitoException){conflito=true;}check(conflito,"Mudança na agenda desde a prévia exige nova conferência");
  check((await svc.UltimaAsync(dia,"Piloto"))!.MinutosAgendados==95,"Alteração posterior da agenda não reescreve fotografia já publicada");
  var retirada=Input(dia,"Piloto",1);retirada.Desconsiderar=true;retirada.Motivo="Dia registrado por engano";await svc.RegistrarAsync(retirada,"ficticio");
  var resumo=PilotoService.Resumir(await db.MedicoesPiloto.AsNoTracking().ToListAsync(),"Piloto",dia,dia);
  check(resumo.DiasMedidos==0&&resumo.TaxaFaltas==null,"Desconsiderar a última revisão não ressuscita valores anteriores nem cria dia zero");
  var retorno=Input(dia,"Piloto",2);retorno.AgendaHash=(await svc.AgendaAsync(dia)).Hash;await svc.RegistrarAsync(retorno,"ficticio");
  check(PilotoService.Resumir(await db.MedicoesPiloto.AsNoTracking().ToListAsync(),"Piloto",dia,dia).DiasMedidos==1&&await db.MedicoesPiloto.CountAsync(x=>x.Periodo=="Piloto")==3,"Nova revisão restaura medição desconsiderada e mantém todo o histórico");
  foreach(var text in new[]{"30.50","30,50","0","10000.00"})check(PilotoCustoBinder.Tentar(text,out var n)&&n==decimal.Parse(text.Replace(',','.'),System.Globalization.CultureInfo.InvariantCulture),"Custo monetário preservado com ponto ou vírgula");
  foreach(var text in new[]{"1.000,50","1,000.50","-1","30.501","10001","1e3"})check(!PilotoCustoBinder.Tentar(text,out _),"Custo ambíguo, negativo, excessivo ou com casas extras é recusado");
 }
}
