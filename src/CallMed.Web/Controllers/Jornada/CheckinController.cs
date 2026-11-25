using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using QRCoder;

namespace MKSANCrud.Controllers;

public sealed record CheckinOpcao(int Id,string Profissional,DateTime Data,string Horario,bool Tele);

[Authorize,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CheckinController(
    MKSANContext db,JornadaService jornada,IClinicaClock clock,
    IDataProtectionProvider protection,IConfiguration configuration):Controller
{
    private ITimeLimitedDataProtector TokenProtegido()=>
        protection.CreateProtector("CallMed.Checkin.Presencial.V1").ToTimeLimitedDataProtector();

    private bool TokenValido(string? token)
    {
        if(string.IsNullOrWhiteSpace(token)||token.Length>2048)return false;
        try{return TokenProtegido().Unprotect(token)=="unidade-v1";}
        catch(CryptographicException){return false;}
        catch(FormatException){return false;}
    }

    private void SemCache()
    {
        Response.Headers["Cache-Control"]="no-store";
        Response.Headers["Referrer-Policy"]="no-referrer";
        Response.Headers["X-Robots-Tag"]="noindex, nofollow";
    }

    [Authorize(Roles="Funcionario,Admin")]
    [HttpGet("/checkin/terminal")]
    public IActionResult Terminal(){SemCache();return View();}

    [Authorize(Roles="Funcionario,Admin")]
    [HttpGet("/checkin/qr")]
    public IActionResult Qr()
    {
        SemCache();
        var token=TokenProtegido().Protect("unidade-v1",TimeSpan.FromMinutes(4));
        var origem=(configuration["PublicOrigin"]??configuration["RENDER_EXTERNAL_URL"]??"").TrimEnd('/');
        if(!Uri.TryCreate(origem,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))
            return StatusCode(503,"Configure PublicOrigin com a origem HTTPS da clínica para o QR Code.");
        var link=origem+"/checkin?token="+Uri.EscapeDataString(token);
        using var data=QRCodeGenerator.GenerateQrCode(link,QRCodeGenerator.ECCLevel.Q);
        using var qr=new SvgQRCode(data);
        return Content(qr.GetGraphic(5),"image/svg+xml");
    }

    [Authorize(Roles="Paciente")]
    [HttpGet("/checkin")]
    public async Task<IActionResult> Abrir(string? token,CancellationToken ct)
    {
        SemCache();
        var valido=TokenValido(token);
        ViewBag.Token=valido?token:null;
        ViewBag.Valido=valido;
        var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);
        var consultas=await jornada.Visiveis(User).AsNoTracking()
            .Where(c=>c.Paciente!=null&&c.Paciente.UsuarioId==uid&&c.Data.Date==clock.Hoje
                &&c.Status!=ConsultaStatus.Cancelada&&c.Status!=ConsultaStatus.Realizada&&c.Status!=ConsultaStatus.Ausente)
            .OrderBy(c=>c.Horario).Take(30).ToListAsync(ct);
        var etapas=await db.Jornadas.AsNoTracking().Where(j=>consultas.Select(c=>c.Id).Contains(j.ConsultaId)).ToDictionaryAsync(j=>j.ConsultaId,ct);
        return View(consultas.Select(c=>new CheckinOpcao(c.Id,c.Medico?.Nome??"Profissional",c.Data,c.Horario,etapas.TryGetValue(c.Id,out var j)&&j.Tele)).ToList());
    }

    [Authorize(Roles="Paciente")]
    [HttpPost("/checkin/registrar"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(int id,string? token,CancellationToken ct)
    {
        SemCache();
        if(!TokenValido(token)){TempData["Erro"]="O QR expirou. Escaneie o código atualizado na clínica.";return RedirectToAction(nameof(Abrir));}
        var c=await jornada.Visiveis(User).SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(c==null)return NotFound();
        var erro=await jornada.RegistrarChegadaAutonomaAsync(c,User,ct);
        TempData[erro==null?"Sucesso":"Erro"]=erro??"Chegada registrada. O profissional foi informado pelo painel.";
        return RedirectToAction("Consulta","Jornada",new{id});
    }
}
