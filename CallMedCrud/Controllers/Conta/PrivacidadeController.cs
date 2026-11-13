using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;
[Route("dados-e-privacidade"),Authorize(AuthenticationSchemes="Identity.Application"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PrivacidadeController(MKSANContext db,IDataProtectionProvider dp,JornadaService jornada):Controller {
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet("")]public async Task<IActionResult> Index(CancellationToken ct){var rows=await db.PedidosPrivacidade.AsNoTracking().Where(x=>x.UsuarioId==Uid).OrderByDescending(x=>x.CriadoEm).Take(100).ToListAsync(ct);ViewBag.Respostas=rows.Where(x=>x.RespostaProtegida!=null).ToDictionary(x=>x.Id,x=>dp.CreateProtector("CallMed.Privacidade.v1").Unprotect(x.RespostaProtegida!));return View(rows);}
 [HttpPost("solicitar"),ValidateAntiForgeryToken]public async Task<IActionResult> Solicitar(string tipo,bool confirmar,CancellationToken ct){
  if(!confirmar||tipo is not ("Acesso" or "Correcao" or "Eliminacao" or "Revogacao"))return BadRequest();
  if(!await db.PedidosPrivacidade.AnyAsync(x=>x.UsuarioId==Uid&&x.Tipo==tipo&&x.RespondidoEm==null,ct)){
   var p=new PedidoPrivacidade{UsuarioId=Uid,Tipo=tipo};db.PedidosPrivacidade.Add(p);jornada.Audit(User,"Registrar pedido de privacidade "+p.Id,0);await db.SaveChangesAsync(ct);
  }TempData["Sucesso"]="Pedido registrado. Acompanhe a resposta nesta página.";return RedirectToAction(nameof(Index));
 }
 [Authorize(Roles="Admin"),HttpGet("gestao")]public async Task<IActionResult> Gestao(CancellationToken ct)=>View(await db.PedidosPrivacidade.AsNoTracking().OrderBy(x=>x.RespondidoEm!=null).ThenBy(x=>x.CriadoEm).Take(300).ToListAsync(ct));
 [Authorize(Roles="Admin"),HttpPost("responder"),ValidateAntiForgeryToken]public async Task<IActionResult> Responder(Guid id,Guid versao,string decisao,string resposta,bool confirmar,CancellationToken ct){
  if(!confirmar||decisao is not ("Atendido" or "Parcial" or "Justificado")||string.IsNullOrWhiteSpace(resposta)||resposta.Length>3000)return BadRequest();
  var p=await db.PedidosPrivacidade.SingleOrDefaultAsync(x=>x.Id==id,ct);if(p==null)return NotFound();
  if(p.Versao!=versao||p.RespondidoEm!=null)return Conflict("Este pedido já foi atualizado. Recarregue a página.");
  p.RespostaProtegida=dp.CreateProtector("CallMed.Privacidade.v1").Protect(resposta.Trim());p.RespondidoPor=Uid;p.RespondidoEm=DateTime.UtcNow;p.Estado=decisao;p.Versao=Guid.NewGuid();jornada.Audit(User,"Responder pedido de privacidade "+p.Id,0);
  try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){return Conflict("Outro responsável já respondeu. Recarregue a página.");}
  TempData["Sucesso"]="Resposta disponibilizada ao titular.";return RedirectToAction(nameof(Gestao));
 }
}
