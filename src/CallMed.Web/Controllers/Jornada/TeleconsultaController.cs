using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;

[Authorize(AuthenticationSchemes="Identity.Application,CallMedClinical",Roles = "Paciente,Psicologo,Medico,Funcionario,Admin")]
[Route("Jornada/Tele"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),RequestSizeLimit(65536)]
public sealed class TeleconsultaController(MKSANContext db,JornadaService jornada,TeleconsultaAcessoService acesso,IDataProtectionProvider dp,TeleconsultaIceService ice,TeleconsultaPresencaService presenca):Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    async Task<bool> Permitida(int id,CancellationToken ct) => (await acesso.ObterAsync(id,User,ct))?.PodeEntrar == true;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Sala(int id,CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"]="no-referrer";
        Response.Headers["Permissions-Policy"]="camera=(self), microphone=(self), display-capture=()";
        var estado=await acesso.ObterAsync(id,User,ct);
        if(estado==null){Response.StatusCode=404;return View("~/Views/Jornada/TeleNaoEncontrada.cshtml");}
        if(estado.Participante){jornada.Audit(User,"Preparar consulta online",id);await db.SaveChangesAsync(ct);}
        return View("~/Views/Jornada/Tele.cshtml",estado);
    }

    [HttpGet("{id:int}/estado")]
    public async Task<IActionResult> Estado(int id,CancellationToken ct)
    {
        var estado=await acesso.ObterAsync(id,User,ct);
        return estado==null?NotFound():Json(new{estado=estado.Estado,liberada=estado.PodeEntrar});
    }

    [HttpPost("{id:int}/consentimento"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Consentimento(int id,bool confirmar,CancellationToken ct)
    {
        var estado=await acesso.ObterAsync(id,User,ct);
        if(estado==null)return NotFound();
        if(!estado.PodeConsentir){TempData["Erro"]="Confira a situação da consulta antes de autorizar o vídeo.";return RedirectToAction(nameof(Sala),new{id});}
        var c=await jornada.Visiveis(User).SingleAsync(c=>c.Id==id,ct);
        var erro=await jornada.MoverAsync(c,User,"consentir",confirmar,ct);
        if(erro!=null)TempData["Erro"]=erro;
        return RedirectToAction(nameof(Sala),new{id});
    }

    [HttpPost("{id:int}/entrar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(int id,CancellationToken ct)
    {
        if(!await Permitida(id,ct))return StatusCode(403,new{mensagem="A sala não está liberada. Atualize esta página para conferir o motivo."});
        jornada.Audit(User,"Entrar na chamada online",id);await db.SaveChangesAsync(ct);return Ok();
    }

    [HttpGet("{id:int}/config")]
    public async Task<IActionResult> Config(int id,CancellationToken ct)
    {
        if(!await Permitida(id,ct))return StatusCode(403);
        var settings=ice.Criar();
        if(settings.Erro!=null)return StatusCode(503,new{mensagem=settings.Erro});
        var cursor=await db.SinaisTele.Where(x=>x.ConsultaId==id).MaxAsync(x=>(long?)x.Id,ct)??0;
        return Json(new{iceServers=settings.Servidores,cursor});
    }

    [HttpGet("{id:int}/sinais")]
    public async Task<IActionResult> Sinais(int id,long depois,CancellationToken ct)
    {
        if(!await Permitida(id,ct))return StatusCode(403);
        var lim=DateTime.UtcNow.AddMinutes(-5);
        var rows=await db.SinaisTele.AsNoTracking().Where(x=>x.ConsultaId==id&&x.Id>depois&&x.UsuarioId!=Uid&&x.CriadoEm>=lim).OrderBy(x=>x.Id).Take(100).ToListAsync(ct);
        var p=dp.CreateProtector("CallMed.Tele.v1");return Json(rows.Select(x=>new{x.Id,payload=p.Unprotect(x.Protegido)}));
    }

    [HttpPost("{id:int}/sinais"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Enviar(int id,[FromBody]SinalInput input,CancellationToken ct)
    {
        if(input?.Payload==null||input.Payload.Length>60000)return BadRequest();
        var participante=await acesso.ObterAsync(id,User,ct);
        if(participante?.PodeEntrar!=true)return StatusCode(403);
        try{using var d=JsonDocument.Parse(input.Payload);var t=d.RootElement.GetProperty("tipo").GetString();
            if(t is not ("ready" or "offer" or "answer" or "ice" or "bye"))return BadRequest();
            if(t=="offer"&&!participante.Paciente||t=="answer"&&!participante.Medico)return StatusCode(403);
        }catch(Exception e)when(e is JsonException or KeyNotFoundException or InvalidOperationException){return BadRequest();}
        if(await db.SinaisTele.CountAsync(x=>x.ConsultaId==id&&x.UsuarioId==Uid&&x.CriadoEm>DateTime.UtcNow.AddMinutes(-1),ct)>150)return StatusCode(429);
        db.SinaisTele.Add(new(){ConsultaId=id,UsuarioId=Uid,Protegido=dp.CreateProtector("CallMed.Tele.v1").Protect(input.Payload)});await db.SaveChangesAsync(ct);return Json(new{ok=true});
    }
    [HttpPost("{id:int}/presenca/entrar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPresenca(int id,CancellationToken ct){var sessao=await presenca.EntrarAsync(id,User,ct);return sessao.HasValue?Json(new{sessaoId=sessao.Value}):StatusCode(403);}
    [HttpPost("{id:int}/presenca"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Presenca(int id,[FromBody]PresencaInput input,CancellationToken ct){if(input==null||input.SessaoId==Guid.Empty)return BadRequest();return await presenca.AtualizarAsync(id,input.SessaoId,input.Estado,User,ct)?Ok():StatusCode(410);}
    [HttpPost("{id:int}/presenca/sair"),ValidateAntiForgeryToken]
    public async Task<IActionResult> SairPresenca(int id,[FromBody]PresencaInput input,CancellationToken ct){if(input==null||input.SessaoId==Guid.Empty)return BadRequest();return await presenca.SairAsync(id,input.SessaoId,User,ct)?Ok():NotFound();}
    public sealed record PresencaInput(Guid SessaoId,string Estado="aguardando");
    [HttpPost("{id:int}/atendimento"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Atendimento(int id,string acao,bool confirmar,CancellationToken ct)
    {
        var c=await jornada.Visiveis(User).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(c==null)return NotFound();
        if(!JornadaService.Medico(User,c))return Forbid();
        if(acao is not ("iniciar" or "concluir"))return BadRequest();
        var erro=await jornada.MoverAsync(c,User,acao,confirmar,ct);
        if(Request.Headers.Accept.ToString().Contains("application/json"))
            return erro==null?Json(new{concluida=acao=="concluir"}):Conflict(new{mensagem=erro});
        if(erro!=null)TempData["Erro"]=erro;
        return RedirectToAction(nameof(Sala),new{id});
    }
    public sealed record SinalInput(string Payload);
}
