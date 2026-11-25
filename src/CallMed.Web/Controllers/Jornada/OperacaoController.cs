using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;
[Authorize(Roles="Admin,Funcionario"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class OperacaoController(MKSANContext db,OperacaoService operacao,IClinicaClock clock,JornadaService jornada):Controller {
 [HttpGet]public async Task<IActionResult> Index(DateTime? inicio,DateTime? fim,bool exportar=false,CancellationToken ct=default){
  try{var r=await operacao.MedirAsync(inicio??clock.Hoje.AddDays(-29),fim??clock.Hoje,ct);
   if(exportar){jornada.Audit(User,"Exportar indicadores agregados do piloto",0);await db.SaveChangesAsync(ct);return File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r,new JsonSerializerOptions{WriteIndented=true})),"application/json","CallMed-piloto-"+r.Inicio.ToString("yyyyMMdd")+".json");}
   ViewBag.Rotinas=await db.SaudeRotinas.AsNoTracking().ToDictionaryAsync(x=>x.Nome,ct);ViewBag.Medicoes=await db.MedicoesOperacionais.AsNoTracking().OrderByDescending(x=>x.Data).Take(200).ToListAsync(ct);return View(r);
  }catch(ArgumentException e){return BadRequest(e.Message);}
 }
}
