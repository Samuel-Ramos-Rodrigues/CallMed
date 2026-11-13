using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public sealed class ConsultaLinkService(MKSANContext db,IDataProtectionProvider dp,IConfiguration config){
 public async Task<string?> CriarAsync(Consulta c,CancellationToken ct){var origin=(config["RENDER_EXTERNAL_URL"]??config["Twa:Origin"]??"").TrimEnd('/');if(!Uri.TryCreate(origin,UriKind.Absolute,out var u)||u.Scheme!="https"||u.UserInfo!="")return null;var snapshot=JornadaService.Snapshot(c);var t=await db.LinksConsulta.FirstOrDefaultAsync(x=>x.ConsultaId==c.Id&&x.Snapshot==snapshot&&x.UsadoEm==null&&x.ExpiraEm>DateTime.UtcNow.AddHours(2),ct);if(t==null){t=new(){ConsultaId=c.Id,Snapshot=snapshot,ExpiraEm=DateTime.UtcNow.AddHours(48)};db.LinksConsulta.Add(t);await db.SaveChangesAsync(ct);}return origin+"/consulta-acao?token="+Uri.EscapeDataString(dp.CreateProtector("CallMed.ConsultaLink.v1").Protect(t.Id.ToString()));}
 public Guid? Ler(string? token){try{return Guid.Parse(dp.CreateProtector("CallMed.ConsultaLink.v1").Unprotect(token??""));}catch(Exception e)when(e is System.Security.Cryptography.CryptographicException or FormatException or ArgumentException){return null;}}
}
