using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public sealed class OperacaoService(MKSANContext db,IClinicaClock clock) {
 public static double? Taxa(int numerador,int denominador)=>denominador==0?null:Math.Round(100d*numerador/denominador,1);
 public async Task<PilotoResumo> MedirAsync(DateTime inicio,DateTime fim,CancellationToken ct) {
  inicio=inicio.Date;fim=fim.Date;
  if(fim<inicio||fim>clock.Hoje||(fim-inicio).TotalDays>365)throw new ArgumentException("Use um período de até 366 dias que termine hoje ou antes.");
  var cs=await db.Consultas.AsNoTracking().Where(c=>c.Data>=inicio&&c.Data<fim.AddDays(1)).ToListAsync(ct);
  var ids=cs.Select(c=>c.Id).ToArray();var js=await db.Jornadas.AsNoTracking().Where(j=>ids.Contains(j.ConsultaId)).ToListAsync(ct);
  var inicioUtc=clock.ConverterParaUtc(inicio);var fimUtc=clock.ConverterParaUtc(fim.AddDays(1));
  var ofertas=await db.Ofertas.AsNoTracking().Where(o=>o.CriadoEm>=inicioUtc&&o.CriadoEm<fimUtc).ToListAsync(ct);
  var notas=await db.AvaliacoesAtendimento.AsNoTracking().Where(a=>a.RespondidoEm>=inicioUtc&&a.RespondidoEm<fimUtc&&a.Nota>=1&&a.Nota<=5).Select(a=>a.Nota!.Value).ToListAsync(ct);
  var espera=js.Where(j=>j.ChegadaEm.HasValue&&j.InicioEm>=j.ChegadaEm).Select(j=>(j.InicioEm!.Value-j.ChegadaEm!.Value).TotalMinutes).ToArray();
  var realizadas=cs.Count(c=>c.Status==ConsultaStatus.Realizada);var faltas=cs.Count(c=>c.Status==ConsultaStatus.Ausente);
  return new(inicio,fim,cs.Count,realizadas,faltas,cs.Count(c=>c.Status==ConsultaStatus.Cancelada),cs.Count(c=>!JornadaService.Encerrada(c)),
   Taxa(faltas,realizadas+faltas),js.Count(j=>j.PresencaConfirmadaEm!=null),js.Count(j=>j.CheckInEm!=null),espera.Length,
   espera.Length==0?null:Math.Round(espera.Average(),1),ofertas.Count,ofertas.Count(o=>o.Estado=="Aceita"),Taxa(ofertas.Count(o=>o.Estado=="Aceita"),ofertas.Count),
   notas.Count,notas.Count==0?null:Math.Round(notas.Average(),2));
 }
 public async Task MarcarAsync(string nome,bool sucesso,CancellationToken ct) {
  var agora=DateTime.UtcNow;
  if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SaudeRotinas\" (\"Nome\",\"TentativaEm\",\"SucessoEm\",\"FalhaEm\",\"FalhasConsecutivas\") VALUES ({nome},{agora},{(sucesso?(DateTime?)agora:null)},{(!sucesso?(DateTime?)agora:null)},{(sucesso?0:1)}) ON CONFLICT (\"Nome\") DO UPDATE SET \"TentativaEm\"=EXCLUDED.\"TentativaEm\",\"SucessoEm\"=CASE WHEN {sucesso} THEN EXCLUDED.\"TentativaEm\" ELSE \"SaudeRotinas\".\"SucessoEm\" END,\"FalhaEm\"=CASE WHEN {sucesso} THEN \"SaudeRotinas\".\"FalhaEm\" ELSE EXCLUDED.\"TentativaEm\" END,\"FalhasConsecutivas\"=CASE WHEN {sucesso} THEN 0 ELSE \"SaudeRotinas\".\"FalhasConsecutivas\"+1 END",ct);
  else {var row=await db.SaudeRotinas.FindAsync([nome],ct);if(row==null){row=new(){Nome=nome};db.SaudeRotinas.Add(row);}row.TentativaEm=agora;if(sucesso){row.SucessoEm=agora;row.FalhasConsecutivas=0;}else{row.FalhaEm=agora;row.FalhasConsecutivas++;}await db.SaveChangesAsync(ct);}
 }
 public static async Task RegistrarAsync(IServiceProvider services,string rotina,bool sucesso,CancellationToken ct) {
  // Falha de telemetria nunca impede o processamento do lembrete ou encaixe.
  try{var op=services.GetService<OperacaoService>();if(op!=null)await op.MarcarAsync(rotina,sucesso,ct);}
  catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
  catch(Exception){services.GetService<ILogger<OperacaoService>>()?.LogWarning("Não foi possível registrar a execução da rotina {Rotina}.",rotina);}
 }
}
public sealed record PilotoResumo(DateTime Inicio,DateTime Fim,int Agendadas,int Realizadas,int Faltas,int Canceladas,int SemDesfecho,
 double? TaxaFaltas,int Confirmacoes,int Checkins,int AmostraEspera,double? EsperaMedia,int Ofertas,int Aceites,double? TaxaAceites,int AmostraSatisfacao,double? SatisfacaoMedia);
