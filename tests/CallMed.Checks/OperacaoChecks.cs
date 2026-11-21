using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;
internal static class OperacaoChecks {
 public static async Task RunAsync(Action<bool,string> check){
  await using var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("ops-"+Guid.NewGuid()).Options);var clock=new Clock();var service=new OperacaoService(db,clock);var day=clock.Hoje.AddDays(-1);
  var empty=await service.MedirAsync(day,day,default);check(empty.TaxaFaltas==null&&empty.EsperaMedia==null&&empty.SatisfacaoMedia==null&&empty.TaxaAceites==null,"Ausência de dados não produz taxas ou ganhos inventados");
  db.Consultas.AddRange(new Consulta{Id=1,Data=day,Status=ConsultaStatus.Realizada},new Consulta{Id=2,Data=day,Status=ConsultaStatus.Ausente},new Consulta{Id=3,Data=day,Status=ConsultaStatus.Cancelada},new Consulta{Id=4,Data=day,Status=ConsultaStatus.Confirmada},new Consulta{Id=5,Data=day.AddDays(-10),Status=ConsultaStatus.Ausente});
  db.Jornadas.AddRange(new Jornada{ConsultaId=1,PresencaConfirmadaEm=DateTime.UtcNow,CheckInEm=DateTime.UtcNow,ChegadaEm=DateTime.UtcNow.AddMinutes(-20),InicioEm=DateTime.UtcNow.AddMinutes(-10)},new Jornada{ConsultaId=2,ChegadaEm=DateTime.UtcNow,InicioEm=DateTime.UtcNow.AddMinutes(-1)},new Jornada{ConsultaId=4,Tele=true,ChegadaEm=DateTime.UtcNow.AddMinutes(-30),InicioEm=DateTime.UtcNow.AddMinutes(-10)});
  db.Ofertas.AddRange(new Oferta{CriadoEm=clock.ConverterParaUtc(day).AddHours(2),Estado="Aceita"},new Oferta{CriadoEm=clock.ConverterParaUtc(day).AddHours(3),Estado="Expirada"});
  db.AvaliacoesAtendimento.AddRange(new AvaliacaoAtendimento{Nota=4,RespondidoEm=clock.ConverterParaUtc(day).AddHours(3)},new AvaliacaoAtendimento{Nota=null},new AvaliacaoAtendimento{Nota=5,RespondidoEm=clock.ConverterParaUtc(day).AddDays(-1)});await db.SaveChangesAsync();
  var r=await service.MedirAsync(day,day,default);check(r.Agendadas==4&&r.Realizadas==1&&r.Faltas==1&&r.Canceladas==1&&r.SemDesfecho==1&&r.TaxaFaltas==50,"Taxa de faltas exclui cancelamentos, pendências e consultas de outro período");
  check(r.AmostraEspera==1&&r.EsperaMedia==10,"Espera presencial exclui intervalos negativos e teleconsultas");check(r.Confirmacoes==1&&r.Checkins==1,"Confirmações e check-ins usam as consultas do período");
  check(r.Ofertas==2&&r.Aceites==1&&r.TaxaAceites==50,"Aceites de ofertas têm denominador próprio");check(r.AmostraSatisfacao==1&&r.SatisfacaoMedia==4,"Satisfação inclui apenas respostas válidas realizadas no período");
  foreach(var (a,b) in new[]{(day,day.AddDays(-1)),(day,clock.Hoje.AddDays(1)),(day.AddDays(-400),day)}){var rejected=false;try{await service.MedirAsync(a,b,default);}catch(ArgumentException){rejected=true;}check(rejected,"Período inválido de piloto é recusado");}
  await service.MarcarAsync("teste",true,default);await service.MarcarAsync("teste",false,default);var h=await db.SaudeRotinas.SingleAsync();check(h.SucessoEm!=null&&h.FalhaEm!=null&&h.FalhasConsecutivas==1,"Falha de rotina preserva data do último sucesso");await service.MarcarAsync("teste",true,default);check(h.FalhasConsecutivas==0,"Nova execução bem-sucedida recupera a saúde da rotina");
 }
 sealed class Clock:IClinicaClock{public DateTime Hoje=>new(2026,11,8);public DateTime Agora=>Hoje.AddHours(12);public DateTime ConverterUtc(DateTime utc)=>DateTime.SpecifyKind(utc.AddHours(-3),DateTimeKind.Unspecified);public DateTime ConverterParaUtc(DateTime local)=>DateTime.SpecifyKind(local.AddHours(3),DateTimeKind.Utc);}
}
