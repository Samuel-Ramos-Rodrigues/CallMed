using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Mobile;
namespace MKSANCrud.Controllers.Mobile;
[ApiController,Route("api/mobile/v1/privacidade"),Authorize(Policy=MobileAuth.Policy),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MobilePrivacyController(MKSANContext db,IDataProtectionProvider dp):ControllerBase {
 string Uid=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet]public async Task<IActionResult> Listar(CancellationToken ct){var rows=await db.PedidosPrivacidade.AsNoTracking().Where(x=>x.UsuarioId==Uid).OrderByDescending(x=>x.CriadoEm).Take(100).ToListAsync(ct);return Ok(rows.Select(x=>new{x.Id,x.Tipo,x.Estado,x.CriadoEm,x.RespondidoEm,resposta=x.RespostaProtegida==null?null:dp.CreateProtector("CallMed.Privacidade.v1").Unprotect(x.RespostaProtegida)}));}
 [HttpPost]public async Task<IActionResult> Solicitar([FromBody]PedidoInput input,CancellationToken ct){if(!input.Confirmar||input.Tipo is not ("Acesso" or "Correcao" or "Eliminacao" or "Revogacao"))return BadRequest(new{mensagem="Escolha e confirme o pedido."});var p=await db.PedidosPrivacidade.SingleOrDefaultAsync(x=>x.UsuarioId==Uid&&x.Tipo==input.Tipo&&x.RespondidoEm==null,ct);if(p==null){p=new(){UsuarioId=Uid,Tipo=input.Tipo};db.PedidosPrivacidade.Add(p);db.AuditoriaEventos.Add(new(){UsuarioId=Uid,Acao="Registrar pedido de privacidade "+p.Id,Entidade="Conta",EntidadeId=Uid});await db.SaveChangesAsync(ct);}return Ok(new{id=p.Id,mensagem="Pedido registrado. Acompanhe a resposta nesta área."});}
 public sealed record PedidoInput(string Tipo,bool Confirmar);
}
