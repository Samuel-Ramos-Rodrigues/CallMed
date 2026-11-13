using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Mobile;
namespace MKSANCrud.Controllers;
public sealed class ClinicalHandoffController(MKSANContext db,IDataProtectionProvider dp,MobileSessionService sessions,JornadaService jornada):Controller {
 [AllowAnonymous,HttpGet("/Jornada/Abrir"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]public async Task<IActionResult> Abrir(string ticket,CancellationToken ct){Response.Headers["Referrer-Policy"]="no-referrer";Response.Headers["X-Robots-Tag"]="noindex, nofollow";try{var id=Guid.Parse(dp.CreateProtector("CallMed.ClinicalTicket.v1").Unprotect(ticket));await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await jornada.LockAsync("clinical-ticket:"+id,ct);var t=await db.MobileTickets.SingleOrDefaultAsync(x=>x.Id==id,ct);if(t==null||t.Usado||t.ExpiraEm<=DateTime.UtcNow)return Unauthorized();var claims=JsonSerializer.Deserialize<string[][]>(dp.CreateProtector("CallMed.ClinicalPrincipal.v1").Unprotect(t.Principal))!;var identity=new ClaimsIdentity(claims.Select(x=>new Claim(x[0],x[1])),"CallMedClinical");var principal=new ClaimsPrincipal(identity);var pid=await sessions.ValidarAsync(principal,ct);if(pid==null||!await db.Consultas.AnyAsync(c=>c.Id==t.ConsultaId&&c.PacienteId==pid,ct))return Unauthorized();identity.AddClaim(new("clinical.consulta",t.ConsultaId.ToString()));t.Usado=true;await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);await HttpContext.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);await HttpContext.SignInAsync("CallMedClinical",principal,new(){IsPersistent=false,ExpiresUtc=DateTimeOffset.UtcNow.AddHours(6)});return RedirectToAction("Consulta","Jornada",new{id=t.ConsultaId});}catch(Exception e)when(e is CryptographicException or JsonException or ArgumentException or FormatException){return Unauthorized();}}
 [Authorize(AuthenticationSchemes="CallMedClinical"),HttpPost("/Jornada/EncerrarAcesso"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Encerrar(){await ClinicalAccess.EncerrarAsync(HttpContext);return Redirect("/acesso");}
}
