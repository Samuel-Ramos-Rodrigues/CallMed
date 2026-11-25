using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public sealed class TeleconsultaPresencaService(MKSANContext db,JornadaService jornada,TeleconsultaAcessoService acesso,TimeProvider time) {
 public const int ValidadeSegundos=60;
 public DateTime Agora=>time.GetUtcNow().UtcDateTime;
 public static bool Presente(SessaoTele s,Consulta c,DateTime agora)=>s.SaidaEm==null&&s.Snapshot==JornadaService.Snapshot(c)&&!JornadaService.Encerrada(c)&&s.UltimoSinalEm>=agora.AddSeconds(-ValidadeSegundos);
 public async Task<Guid?> EntrarAsync(int id,ClaimsPrincipal user,CancellationToken ct) {
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
  await jornada.LockAsync("jornada:"+id,ct);var a=await acesso.ObterAsync(id,user,ct);if(a?.PodeEntrar!=true)return null;
  var uid=user.FindFirstValue(ClaimTypes.NameIdentifier)!;var agora=Agora;
  foreach(var s in await db.SessoesTele.Where(x=>x.ConsultaId==id&&x.UsuarioId==uid&&x.SaidaEm==null).ToListAsync(ct)){s.SaidaEm=agora;s.MotivoSaida="Nova entrada neste atendimento";}
  var sessao=new SessaoTele{ConsultaId=id,UsuarioId=uid,Papel=a.Paciente?"Paciente":"Profissional",Snapshot=JornadaService.Snapshot(a.Consulta),EntradaEm=agora,UltimoSinalEm=agora};
  db.SessoesTele.Add(sessao);
  // Um paciente que já conferiu o cadastro não precisa repetir o botão
  // de check-in: a entrada autenticada na sala realiza a etapa digital.
  // Não registrar chegada presencial; ela permanece um evento distinto.
  if(a.Paciente)
  {
    var etapa=await db.Jornadas.SingleOrDefaultAsync(j=>j.ConsultaId==id,ct);
    if(etapa?.CadastroEm!=null && etapa.CheckInEm==null)
    {
      etapa.CheckInEm=agora;
      etapa.AtualizadoEm=agora;
      jornada.Audit(user,"Check-in digital automático por entrada na teleconsulta",id);
    }
  }
  jornada.Audit(user,"Entrar na sala online",id);await db.SaveChangesAsync(ct);
  if(tx!=null)await tx.CommitAsync(ct);return sessao.Id;
 }
 public async Task<bool> AtualizarAsync(int id,Guid sessaoId,string estado,ClaimsPrincipal user,CancellationToken ct) {
  if(estado is not ("aguardando" or "conectando" or "conectada" or "interrompida"))return false;
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
  await jornada.LockAsync("jornada:"+id,ct);var a=await acesso.ObterAsync(id,user,ct);if(a?.PodeEntrar!=true)return false;
  var uid=user.FindFirstValue(ClaimTypes.NameIdentifier);var s=await db.SessoesTele.SingleOrDefaultAsync(x=>x.Id==sessaoId&&x.ConsultaId==id&&x.UsuarioId==uid,ct);
  if(s==null||!Presente(s,a.Consulta,Agora))return false;s.UltimoSinalEm=Agora;s.Estado=estado;if(estado=="conectada")s.ConectadaEm??=Agora;
  await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return true;
 }
 public async Task<bool> SairAsync(int id,Guid sessaoId,ClaimsPrincipal user,CancellationToken ct) {
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await jornada.LockAsync("jornada:"+id,ct);
  var uid=user.FindFirstValue(ClaimTypes.NameIdentifier);var s=await db.SessoesTele.SingleOrDefaultAsync(x=>x.Id==sessaoId&&x.ConsultaId==id&&x.UsuarioId==uid,ct);if(s==null)return false;
  if(s.SaidaEm==null){s.SaidaEm=Agora;s.MotivoSaida="Saída informada pelo participante";jornada.Audit(user,"Sair da sala online",id);await db.SaveChangesAsync(ct);}
  if(tx!=null)await tx.CommitAsync(ct);return true;
 }
}
