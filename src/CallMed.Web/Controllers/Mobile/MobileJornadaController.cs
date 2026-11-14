using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Mobile;
namespace MKSANCrud.Controllers.Mobile;
[ApiController,Route("api/mobile/v1/jornada"),Authorize(Policy=MobileAuth.Policy),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MobileJornadaController(MKSANContext db,JornadaService jornada,AgendamentoService agenda,EncaixeService encaixe,ListaEsperaService espera,IDataProtectionProvider dp,IConfiguration config):ControllerBase {
 int Pid=>(int)HttpContext.Items[MobileAuth.PacienteItem]!;
 [HttpGet("{id:int}")]public async Task<IActionResult> Detalhe(int id,CancellationToken ct){var c=await db.Consultas.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id&&c.PacienteId==Pid,ct);if(c==null)return NotFound();var j=await db.Jornadas.AsNoTracking().Include(x=>x.Sala).SingleOrDefaultAsync(x=>x.ConsultaId==id,ct);return Ok(new{etapa=JornadaService.Encerrada(c)?c.Status:JornadaService.Etapa(j),presencaConfirmada=j?.PresencaConfirmadaEm!=null,cadastroConferido=j?.CadastroEm!=null,checkIn=j?.CheckInEm!=null,chegada=j?.ChegadaEm!=null,teleconsulta=j?.Tele??false,sala=j?.Sala?.Nome,encerrada=JornadaService.Encerrada(c)});}
 [HttpPost("{id:int}/{acao}")]public async Task<IActionResult> Mover(int id,string acao,CancellationToken ct){if(acao is not ("cadastro" or "checkin" or "confirmar"))return BadRequest();var c=await db.Consultas.Include(c=>c.Paciente).Include(c=>c.Medico).SingleOrDefaultAsync(c=>c.Id==id&&c.PacienteId==Pid,ct);if(c==null)return NotFound();var error=await jornada.MoverAsync(c,User,acao,true,ct);return error==null?Ok(new{mensagem="Etapa atualizada."}):BadRequest(new{mensagem=error});}
 [HttpPost("{id:int}/portal")]public async Task<IActionResult> Portal(int id,CancellationToken ct){if(!await db.Consultas.AnyAsync(c=>c.Id==id&&c.PacienteId==Pid,ct))return NotFound();var origin=(config["RENDER_EXTERNAL_URL"]??config["Twa:Origin"]??"").TrimEnd('/');if(!Uri.TryCreate(origin,UriKind.Absolute,out var u)||u.Scheme!="https"||u.UserInfo!="")return StatusCode(503,new{mensagem="Endereço seguro da clínica ainda não configurado."});var t=new MobileTicket{ConsultaId=id,ExpiraEm=DateTime.UtcNow.AddMinutes(2),Principal=dp.CreateProtector("CallMed.ClinicalPrincipal.v1").Protect(JsonSerializer.Serialize(User.Claims.Select(c=>new[]{c.Type,c.Value})))};db.MobileTickets.Add(t);await db.SaveChangesAsync(ct);return Ok(new{url=origin+"/Jornada/Abrir?ticket="+Uri.EscapeDataString(dp.CreateProtector("CallMed.ClinicalTicket.v1").Protect(t.Id.ToString()))});}
 [HttpGet("ofertas")]public async Task<IActionResult> Ofertas(CancellationToken ct)=>Ok(await db.Ofertas.AsNoTracking().Where(x=>x.ListaEspera.PacienteId==Pid&&x.ListaEspera.Ativa&&x.Estado=="Aberta"&&x.ExpiraEm>DateTime.UtcNow).Select(x=>new{x.Id,data=x.Data.ToString("yyyy-MM-dd"),x.Horario,x.ExpiraEm,medico=db.Medicos.Where(m=>m.Id==x.MedicoId).Select(m=>m.Nome).FirstOrDefault()}).ToListAsync(ct));
 [HttpPost("ofertas/{id:guid}")]public async Task<IActionResult> Oferta(Guid id,[FromBody]OfertaInput input,CancellationToken ct){var o=await db.Ofertas.Include(x=>x.ListaEspera).SingleOrDefaultAsync(x=>x.Id==id&&x.ListaEspera.PacienteId==Pid,ct);if(o==null)return NotFound();if(o.Estado!="Aberta"||o.ExpiraEm<=DateTime.UtcNow)return BadRequest(new{mensagem="Oferta expirada."});if(!input.Aceitar)return await encaixe.RecusarAsync(id,Pid,ct)?Ok(new{mensagem="Oferta recusada. Você continua na lista."}):BadRequest(new{mensagem="A oferta já mudou."});var r=await agenda.AgendarAsync(Pid,o.MedicoId,o.Data,o.Horario,"Encaixe aceito no Android",ct:ct,ofertaId:id);if(!r.Sucesso)return BadRequest(new{mensagem=r.Mensagem});jornada.Audit(User,"Aceitar vaga",r.Consulta!.Id);await db.SaveChangesAsync(ct);return Ok(new{mensagem=r.Mensagem});}
 [HttpPost("espera")]public async Task<IActionResult> Espera([FromBody]EsperaInput input,CancellationToken ct){try{await espera.AdicionarAsync(Pid,input.MedicoId,null,null,input.Periodo,null,ct);return Ok(new{mensagem="Pedido registrado na lista de espera."});}catch(InvalidOperationException e){return BadRequest(new{mensagem=e.Message});}}
 public sealed record OfertaInput(bool Aceitar);public sealed record EsperaInput(int MedicoId,string Periodo);
}
