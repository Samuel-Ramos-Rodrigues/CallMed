using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

// Cada item representa uma excecao real que exige providencia da equipe.
public sealed record PendenciaRecepcaoItem(int ConsultaId,string Paciente,string Profissional,string Horario,string Motivo,string Acao);
public sealed record PendenciasRecepcaoPage(DateTime Dia,int ConsultasHoje,int ChegadasConfirmadas,int ProntosOnline,IReadOnlyList<PendenciaRecepcaoItem> Pendencias);

[Authorize(Roles="Funcionario,Admin"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RecepcaoPendenciasController(MKSANContext db,IClinicaClock clock):Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var dia=clock.Hoje;
        var consultas=await db.Consultas.AsNoTracking().Include(c=>c.Paciente).Include(c=>c.Medico)
            .Where(c=>c.Data.Date==dia&&c.Status!=ConsultaStatus.Cancelada&&c.Status!=ConsultaStatus.Realizada&&c.Status!=ConsultaStatus.Ausente)
            .OrderBy(c=>c.Horario).Take(500).ToListAsync(ct);
        var ids=consultas.Select(c=>c.Id).ToArray();
        var etapas=await db.Jornadas.AsNoTracking().Where(j=>ids.Contains(j.ConsultaId))
            .ToDictionaryAsync(j=>j.ConsultaId,ct);
        var itens=new List<PendenciaRecepcaoItem>();
        var chegadas=0;var online=0;
        foreach(var c in consultas)
        {
            etapas.TryGetValue(c.Id,out var j);
            if(j?.ChegadaEm!=null)chegadas++;
            if(j?.Tele==true&&j.CheckInEm!=null)online++;
            // Acoes concluidas e check-ins online nao poluem a lista da recepcao.
            if(j?.InicioEm!=null||j?.FimEm!=null)continue;
            string? motivo=null;
            string acao="Consulta";
            if(j?.CadastroEm==null){motivo="Pré-cadastro ainda não conferido";acao="Jornada";}
            else if(j.Tele && j.ConsentimentoEm==null){motivo="Autorização de teleconsulta pendente";acao="Jornada";}
            else if(!j.Tele&&j.ChegadaEm==null&&j.CheckInEm!=null){motivo="Pré-check-in realizado; chegada presencial ainda não registrada";acao="Jornada";}
            if(motivo!=null)itens.Add(new(c.Id,c.Paciente?.Nome??"Paciente",c.Medico?.Nome??"Profissional",c.Horario,motivo,acao));
        }
        ViewData["Title"]="Pendências de hoje";
        return View(new PendenciasRecepcaoPage(dia,consultas.Count,chegadas,online,itens));
    }
}
