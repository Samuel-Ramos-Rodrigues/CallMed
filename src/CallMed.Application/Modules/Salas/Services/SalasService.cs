using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public sealed class SalasService(MKSANContext db,JornadaService jornada,IClinicaClock clock) {
 public static bool Ocupa(Consulta c)=>c.Status is not (ConsultaStatus.Cancelada or ConsultaStatus.Ausente);
 public static DateTime? Inicio(Consulta c)=>TimeSpan.TryParse(c.Horario,CultureInfo.InvariantCulture,out var h)&&h>=TimeSpan.Zero&&h<TimeSpan.FromDays(1)?c.Data.Date.Add(h):null;
 public async Task<string?> SalvarAsync(int? id,string? nome,string? localizacao,string? recursos,bool ativa,ClaimsPrincipal user,CancellationToken ct) {
  if(!JornadaService.Equipe(user))return "Somente a equipe pode organizar as salas.";
  nome=nome?.Trim();localizacao=localizacao?.Trim()??"";recursos=recursos?.Trim()??"";
  if(string.IsNullOrEmpty(nome)||nome.Length>80||localizacao.Length>120||recursos.Length>500)return "Confira o nome (até 80 caracteres), a localização (120) e os recursos (500).";
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await jornada.LockAsync("salas-cadastro",ct);if(id.HasValue)await jornada.LockAsync("sala:"+id,ct);
  var s=id.HasValue?await db.Salas.SingleOrDefaultAsync(x=>x.Id==id,ct):new SalaAtendimento();if(s==null)return "Sala não encontrada.";
  if(db.Database.IsRelational()&&id.HasValue)await db.Entry(s).ReloadAsync(ct);
  if((await db.Salas.Where(x=>x.Id!=(id??0)).Select(x=>x.Nome).ToListAsync(ct)).Any(x=>string.Equals(x.Trim(),nome,StringComparison.OrdinalIgnoreCase)))return "Já existe uma sala com esse nome.";
  if(!ativa&&s.Ativa&&id.HasValue) {
   var reservas=await db.Jornadas.Include(x=>x.Consulta).Where(x=>x.SalaId==id&&x.FimEm==null&&x.Consulta.Status!=ConsultaStatus.Cancelada&&x.Consulta.Status!=ConsultaStatus.Ausente&&x.Consulta.Status!=ConsultaStatus.Realizada).ToListAsync(ct);
   if(reservas.Any(x=>Inicio(x.Consulta)?.AddMinutes(x.Duracao)>clock.Agora||x.InicioEm!=null))return "Transfira as reservas antes de desativar a sala.";
  }
  s.Nome=nome;s.Localizacao=localizacao;s.Recursos=recursos;s.Ativa=ativa;if(!id.HasValue)db.Salas.Add(s);await db.SaveChangesAsync(ct);
  jornada.Audit(user,"Salvar sala física "+s.Id,0);await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return null;
 }
 public async Task<string?> BloquearAsync(int salaId,DateTime inicio,DateTime fim,string? motivo,ClaimsPrincipal user,CancellationToken ct) {
  if(!JornadaService.Equipe(user))return "Somente a equipe pode bloquear salas.";
  inicio=DateTime.SpecifyKind(inicio,DateTimeKind.Unspecified);fim=DateTime.SpecifyKind(fim,DateTimeKind.Unspecified);motivo=motivo?.Trim();
  if(inicio<clock.Hoje||fim<=inicio||fim<=clock.Agora||fim>clock.Hoje.AddYears(2)||string.IsNullOrEmpty(motivo)||motivo.Length>200)return "Confira o período e informe um motivo de até 200 caracteres.";
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await jornada.LockAsync("sala:"+salaId,ct);
  if(!await db.Salas.AnyAsync(x=>x.Id==salaId&&x.Ativa,ct))return "Sala indisponível.";
  if(await db.BloqueiosSala.AnyAsync(x=>x.SalaId==salaId&&x.CanceladoEm==null&&x.Inicio<fim&&x.Fim>inicio,ct))return "Já existe bloqueio em parte desse período.";
  var reservas=await db.Jornadas.Include(x=>x.Consulta).Where(x=>x.SalaId==salaId&&x.Consulta.Data>=inicio.Date.AddDays(-1)&&x.Consulta.Data<=fim.Date&&x.Consulta.Status!=ConsultaStatus.Cancelada&&x.Consulta.Status!=ConsultaStatus.Ausente).ToListAsync(ct);
  if(reservas.Any(x=>Inicio(x.Consulta) is DateTime at&&at<fim&&at.AddMinutes(x.Duracao)>inicio))return "Transfira as reservas antes de bloquear a sala.";
  db.BloqueiosSala.Add(new(){SalaId=salaId,Inicio=inicio,Fim=fim,Motivo=motivo});jornada.Audit(user,"Bloquear sala física "+salaId,0);await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return null;
 }
 public async Task<string?> LiberarAsync(int id,ClaimsPrincipal user,CancellationToken ct) {
  if(!JornadaService.Equipe(user))return "Somente a equipe pode liberar bloqueios.";
  var b=await db.BloqueiosSala.SingleOrDefaultAsync(x=>x.Id==id,ct);if(b==null)return "Bloqueio não encontrado.";
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await jornada.LockAsync("sala:"+b.SalaId,ct);if(db.Database.IsRelational())await db.Entry(b).ReloadAsync(ct);
  if(b.CanceladoEm==null){b.CanceladoEm=DateTime.UtcNow;jornada.Audit(user,"Liberar bloqueio de sala "+id,0);await db.SaveChangesAsync(ct);}if(tx!=null)await tx.CommitAsync(ct);return null;
 }
}
