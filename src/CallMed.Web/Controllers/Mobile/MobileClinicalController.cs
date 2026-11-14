using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Agendamento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Mobile;
namespace MKSANCrud.Controllers.Mobile;

// Adaptador explícito para a API bearer. Reutiliza as operações clínicas do site;
// não aceita cookies nem desabilita o antiforgery das rotas Razor.
[ApiController, Route("api/mobile/v1/clinico"), Authorize(Policy=MobileAuth.Policy)]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None), RequestSizeLimit(180000)]
public sealed class MobileClinicalController(MKSANContext db, JornadaService jornada,
 AgendamentoService agenda, EncaixeService encaixe, IClinicaClock clock,
 TeleconsultaAcessoService acesso, IDataProtectionProvider dp, TeleconsultaIceService ice,TeleconsultaPresencaService presenca) : ControllerBase
{
 JornadaController Preparacao() => new(db,jornada,agenda,encaixe,clock){ControllerContext=ControllerContext};
 TeleconsultaController Tele() => new(db,jornada,acesso,dp,ice,presenca){ControllerContext=ControllerContext};
 int Pid => (int)HttpContext.Items[MobileAuth.PacienteItem]!;
 async Task<bool> Minha(int id,CancellationToken ct) => await db.Consultas.AnyAsync(c=>c.Id==id&&c.PacienteId==Pid,ct);
 [HttpGet("{id:int}")] public async Task<IActionResult> Dados(int id,CancellationToken ct) =>
  !await Minha(id,ct)?NotFound():await Preparacao().Clinico(id,ct);
 [HttpPost("chave")] public Task<IActionResult> Chave([FromBody]JornadaController.ChaveInput input,CancellationToken ct) => Preparacao().Chave(input,ct);
 [HttpPost("{id:int}/resposta")] public async Task<IActionResult> Resposta(int id,[FromBody]JornadaController.RespostaInput input,CancellationToken ct) =>
  !await Minha(id,ct)?NotFound():Normalizar(await Preparacao().Resposta(id,input,ct));
 IActionResult Normalizar(IActionResult r)=>r is StatusCodeResult{StatusCode:403}?Conflict(new{mensagem="A preparação não pode ser alterada após o início ou encerramento da sessão."}):r;
 [HttpGet("{id:int}/registros")] public async Task<IActionResult> Registros(int id,CancellationToken ct) =>
  !await Minha(id,ct)?NotFound():await Preparacao().Registros(id,ct);
 [HttpGet("{id:int}/tele")] public async Task<IActionResult> Estado(int id,CancellationToken ct) {
  if(!await Minha(id,ct))return NotFound();
  var s=await acesso.ObterAsync(id,User,ct); if(s==null)return NotFound();
  return Ok(new{estado=s.Estado,liberada=s.PodeEntrar,podeConsentir=s.PodeConsentir,s.Bloqueio,s.Inicio,s.Abertura,s.Encerramento,
   consentimento=new{versao=ConsentimentoAtendimento.Versao(s.Consulta),texto=ConsentimentoAtendimento.Texto(s.Consulta)}});
 }
 [HttpPost("{id:int}/tele/consentimento")] public async Task<IActionResult> Consentimento(int id,[FromBody]ConsentirInput input,CancellationToken ct) {
  if(!await Minha(id,ct))return NotFound(); var s=await acesso.ObterAsync(id,User,ct);
  if(s==null)return NotFound();
  if(!input.Confirmar||input.Versao!=ConsentimentoAtendimento.Versao(s.Consulta))return BadRequest(new{mensagem="Leia e aceite o consentimento atual antes de continuar."});
  if(s.Consentiu)return Ok(new{mensagem="Consentimento já registrado."});
  if(!s.PodeConsentir)return Conflict(new{mensagem=s.Bloqueio??"A consulta não permite consentir neste momento."});
  var c=await jornada.Visiveis(User).SingleAsync(c=>c.Id==id,ct);
  var e=await jornada.MoverAsync(c,User,"consentir",true,ct);
  return e==null?Ok(new{mensagem="Consentimento registrado."}):Conflict(new{mensagem=e});
 }
 async Task<IActionResult?> Bloqueio(int id,CancellationToken ct) {
  if(!await Minha(id,ct))return NotFound();var s=await acesso.ObterAsync(id,User,ct);
  return s?.PodeEntrar==true?null:Conflict(new{mensagem=s?.Bloqueio??"A sala ainda não está liberada. Confira o consentimento e o horário da consulta."});
 }
 [HttpGet("{id:int}/tele/config")] public async Task<IActionResult> Config(int id,CancellationToken ct) => await Bloqueio(id,ct)??await Tele().Config(id,ct);
 [HttpPost("{id:int}/tele/entrar")] public async Task<IActionResult> Entrar(int id,CancellationToken ct) {
  var b=await Bloqueio(id,ct);if(b!=null)return b;var r=await Tele().Entrar(id,ct);return r is OkResult?Ok(new{ok=true}):Conflict(new{mensagem="A liberação da sala mudou. Atualize a situação."});
 }
 [HttpGet("{id:int}/tele/sinais")] public async Task<IActionResult> Sinais(int id,long depois,CancellationToken ct) => await Bloqueio(id,ct)??await Tele().Sinais(id,depois,ct);
 [HttpPost("{id:int}/tele/sinais")] public async Task<IActionResult> Sinal(int id,[FromBody]TeleconsultaController.SinalInput input,CancellationToken ct) => await Bloqueio(id,ct)??await Tele().Enviar(id,input,ct);
 [HttpPost("{id:int}/tele/presenca/entrar")] public async Task<IActionResult> RegistrarPresenca(int id,CancellationToken ct)=>!await Minha(id,ct)?NotFound():await Tele().RegistrarPresenca(id,ct);
 [HttpPost("{id:int}/tele/presenca")] public async Task<IActionResult> Presenca(int id,[FromBody]TeleconsultaController.PresencaInput input,CancellationToken ct)=>!await Minha(id,ct)?NotFound():await Tele().Presenca(id,input,ct);
 [HttpPost("{id:int}/tele/presenca/sair")] public async Task<IActionResult> SairPresenca(int id,[FromBody]TeleconsultaController.PresencaInput input,CancellationToken ct)=>!await Minha(id,ct)?NotFound():await Tele().SairPresenca(id,input,ct);
 public sealed record ConsentirInput(bool Confirmar,string Versao);
}
