using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;

[Authorize(Roles="Admin,Funcionario"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PilotoController(MKSANContext db,PilotoService piloto,IClinicaClock clock,JornadaService jornada):Controller {
 [HttpGet] public async Task<IActionResult> Index(DateTime? antesInicio,DateTime? antesFim,DateTime? pilotoInicio,DateTime? pilotoFim,bool exportar=false,CancellationToken ct=default) {
  try {
   if(!ModelState.IsValid)throw new ArgumentException("Confira as datas dos dois períodos.");
   var relatorio=await piloto.RelatorioAsync(antesInicio??clock.Hoje.AddDays(-60),antesFim??clock.Hoje.AddDays(-31),pilotoInicio??clock.Hoje.AddDays(-30),pilotoFim??clock.Hoje.AddDays(-1),ct);
   var r=relatorio.Comparacao;var ultimas=relatorio.Medicoes;
   if(exportar) {
    jornada.Audit(User,"Exportar comparação agregada do piloto",0);await db.SaveChangesAsync(ct);
    var evidencia=new{GeradoEm=DateTime.UtcNow,Metodo="CallMed-V27-1",Comparacao=r,
     Definicoes=new{Faltas="Faltas / (realizadas + faltas).",Ocupacao="Minutos agendados não cancelados / minutos de capacidade profissional disponível. Não é presença efetiva.",
      Recepcao="Minutos e custo por agendamento (inclui cancelados), somente nos dias com respectivas medições.",Cobertura="Dias sem medição não são zero. Comparação descritiva; não demonstra causalidade."},
     Medicoes=ultimas.Select(x=>new{x.Data,x.Periodo,x.Revisao,x.Realizadas,x.Faltas,x.Canceladas,x.SemDesfecho,x.MinutosAgendados,x.MinutosDisponiveis,x.MinutosRecepcao,x.CustoHoraRecepcao,x.Fonte,x.Motivo,x.AgendaHash,x.RegistradoEm})};
    return File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evidencia,new JsonSerializerOptions{WriteIndented=true})),"application/json","CallMed-comparacao-piloto.json");
   }
   ViewBag.Medicoes=ultimas;return View(r);
  } catch(ArgumentException e){ModelState.AddModelError("",e.Message);Response.StatusCode=400;return View(model:null);}
 }
 [HttpGet] public async Task<IActionResult> Medicao(DateTime? data,string periodo="Piloto",CancellationToken ct=default) {
  if(!ModelState.IsValid)return BadRequest("Confira a data informada.");
  var dia=data??clock.Hoje.AddDays(-1);
  try {
   piloto.ValidarDia(dia,periodo);var anterior=await piloto.UltimaAsync(dia,periodo,ct);
   var input=new MedicaoPilotoInput{Data=dia.Date,Periodo=periodo,RevisaoAnterior=anterior?.Revisao??0,
    Realizadas=anterior?.Realizadas,Faltas=anterior?.Faltas,Canceladas=anterior?.Canceladas,SemDesfecho=anterior?.SemDesfecho,
    MinutosAgendados=anterior?.MinutosAgendados,MinutosDisponiveis=anterior?.MinutosDisponiveis,
    MinutosRecepcao=anterior?.MinutosRecepcao,CustoHoraRecepcao=anterior?.CustoHoraRecepcao,Fonte=anterior?.Fonte??""};
   return await Formulario(input,ct,atualizarHash:true);
  } catch(ArgumentException e){return BadRequest(e.Message);}
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Medicao([Bind(Prefix="Input")] MedicaoPilotoInput input,CancellationToken ct=default) {
  try {
   piloto.ValidarDia(input.Data,input.Periodo);
   if(!ModelState.IsValid)throw new ArgumentException("Confira os campos informados. Campos em branco significam medição desconhecida.");
   jornada.Audit(User,"Registrar revisão de medição do piloto",0);
   var row=await piloto.RegistrarAsync(input,User.FindFirstValue(ClaimTypes.NameIdentifier)??"",ct);
   TempData["PilotoSalvo"]="Medição registrada. A comparação usa a revisão mais recente de cada dia.";
   return RedirectToAction(nameof(Medicao),new{data=row.Data.ToString("yyyy-MM-dd"),periodo=row.Periodo});
  } catch(MedicaoConflitoException e){ModelState.AddModelError("",e.Message);Response.StatusCode=409;return await Formulario(input,ct);}
    catch(ArgumentException e){ModelState.AddModelError("",e.Message);Response.StatusCode=400;
     // Uma data/período inválido não dispara consulta da agenda.
     try{piloto.ValidarDia(input.Data,input.Periodo);}catch(ArgumentException){return BadRequest(e.Message);}
     return await Formulario(input,ct);
    }
 }
 private async Task<IActionResult> Formulario(MedicaoPilotoInput input,CancellationToken ct,bool atualizarHash=false) {
  var agenda=input.Periodo=="Piloto"?await piloto.AgendaAsync(input.Data,ct):null;
  if(atualizarHash&&agenda!=null)input.AgendaHash=agenda.Hash;
  var historico=await db.MedicoesPiloto.AsNoTracking().Where(x=>x.Data==input.Data.Date&&x.Periodo==input.Periodo).OrderByDescending(x=>x.Revisao).Take(50).ToListAsync(ct);
  return View("Medicao",new MedicaoPilotoPagina(input,agenda,historico,clock.Hoje.AddDays(-1)));
 }
}
public sealed record MedicaoPilotoPagina(MedicaoPilotoInput Input,AgendaPiloto? Agenda,List<MedicaoPiloto> Historico,DateTime UltimoDia);
