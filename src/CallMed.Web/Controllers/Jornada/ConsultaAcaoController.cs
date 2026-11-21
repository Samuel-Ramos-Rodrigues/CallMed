using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Agendamento;
namespace MKSANCrud.Controllers;
[AllowAnonymous,Route("consulta-acao"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),EnableRateLimiting("webhooks")]
public sealed class ConsultaAcaoController(MKSANContext db,ConsultaLinkService links,AgendamentoService agenda,JornadaService jornada,IClinicaClock clock):Controller {
 void Headers(){Response.Headers["Referrer-Policy"]="no-referrer";Response.Headers["X-Robots-Tag"]="noindex, nofollow";}
 async Task<LinkConsulta?> Obter(string token,CancellationToken ct){var id=links.Ler(token);if(id==null)return null;var t=await db.LinksConsulta.SingleOrDefaultAsync(x=>x.Id==id&&x.UsadoEm==null&&x.ExpiraEm>DateTime.UtcNow,ct);if(t==null)return null;var c=await db.Consultas.SingleOrDefaultAsync(x=>x.Id==t.ConsultaId,ct);return c!=null&&!JornadaService.Encerrada(c)&&c.Data.Date>=clock.Hoje&&JornadaService.Snapshot(c)==t.Snapshot?t:null;}
 [HttpGet]public async Task<IActionResult> Index(string token,CancellationToken ct){Headers();var t=await Obter(token,ct);if(t==null)return StatusCode(410,"Link indisponível, utilizado ou expirado. Entre em sua conta CallMed.");var c=await db.Consultas.SingleAsync(x=>x.Id==t.ConsultaId,ct);ViewBag.Token=token;ViewBag.Datas=await agenda.DatasDisponiveisAsync(c.MedicoId,c.Id,ct);return View("~/Views/Jornada/Acao.cshtml",c);}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> Aplicar(string token,string acao,DateTime? data,string? horario,CancellationToken ct){Headers();if(acao is not ("confirmar" or "cancelar" or "remarcar"))return BadRequest();var t=await Obter(token,ct);if(t==null)return StatusCode(410);await using(var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null){await jornada.LockAsync("link:"+t.Id,ct);if(db.Database.IsRelational())await db.Entry(t).ReloadAsync(ct);if(t.UsadoEm!=null||t.ExpiraEm<=DateTime.UtcNow)return StatusCode(410);t.UsadoEm=DateTime.UtcNow;await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);}ResultadoOperacaoConsulta r;
  try{r=acao switch{"confirmar"=>await agenda.ConfirmarAsync(t.ConsultaId,ct,t.Snapshot),"cancelar"=>await agenda.CancelarAsync(t.ConsultaId,ct,t.Snapshot),"remarcar" when data.HasValue&&!string.IsNullOrWhiteSpace(horario)=>await agenda.RemarcarAsync(t.ConsultaId,data.Value,horario,ct,t.Snapshot),_=>new(false,"Escolha data e horário válidos.")};}catch{t.UsadoEm=null;await db.SaveChangesAsync(ct);throw;}
  if(!r.Sucesso){t.UsadoEm=null;await db.SaveChangesAsync(ct);TempData["Erro"]=r.Mensagem;return RedirectToAction(nameof(Index),new{token});}db.AuditoriaEventos.Add(new(){Acao="Ação por link",Entidade="Consulta",EntidadeId=t.ConsultaId.ToString(),Descricao=acao,CriadoEm=DateTime.UtcNow});await db.SaveChangesAsync(ct);ViewBag.Resultado=r.Mensagem;ViewBag.Token=null;return View("~/Views/Jornada/Acao.cshtml",r.Consulta);
 }
 [HttpGet("horarios")]public async Task<IActionResult> Horarios(string token,DateTime data,CancellationToken ct){Headers();var t=await Obter(token,ct);if(t==null)return StatusCode(410);var c=await db.Consultas.SingleAsync(x=>x.Id==t.ConsultaId,ct);return Json(await agenda.HorariosDisponiveisAsync(c.MedicoId,data,c.Id,ct));}
}
