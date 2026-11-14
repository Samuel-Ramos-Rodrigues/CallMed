using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;
[Authorize(Roles="Paciente"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PreparacaoController(MKSANContext db,PreparacaoLinkService links):Controller {
 [HttpGet("/preparacao")]
 public async Task<IActionResult> Abrir(string? token,CancellationToken ct){Response.Headers["Referrer-Policy"]="no-referrer";Response.Headers["X-Robots-Tag"]="noindex, nofollow";var link=links.Ler(token);if(link==null){TempData["Erro"]="O link de preparação expirou. Abra a sessão pela sua conta.";return RedirectToAction("Index","Jornada");}var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);var c=await db.Consultas.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==link.ConsultaId&&c.PacienteId==link.PacienteId&&c.Paciente!.UsuarioId==uid,ct);if(c==null)return NotFound();if(JornadaService.Snapshot(c)!=link.Snapshot){TempData["Erro"]="O agendamento mudou. Confira os dados atuais.";return RedirectToAction("Consulta","Jornada",new{id=c.Id});}return Redirect("/Jornada/Consulta/"+c.Id+"#j-clinico");}
}
