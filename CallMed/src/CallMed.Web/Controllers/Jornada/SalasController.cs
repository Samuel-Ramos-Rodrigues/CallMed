using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;

[Authorize(Roles="Admin,Funcionario,Medico,Psicologo"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SalasController(MKSANContext db,JornadaService jornada,SalasService salas,TeleconsultaPresencaService presenca,IClinicaClock clock):Controller {
 public async Task<IActionResult> Index(DateTime? data,string aba="fisicas",int? profissionalId=null,CancellationToken ct=default) {
  if(!ModelState.IsValid||aba is not ("fisicas" or "online"))return BadRequest();
  var dia=(data??clock.Hoje).Date;if(dia<new DateTime(2000,1,2)||dia>new DateTime(9998,12,30))return BadRequest();
  var equipe=JornadaService.Equipe(User);var q=jornada.Visiveis(User).AsNoTracking().Where(c=>c.Data>=dia.AddDays(-1)&&c.Data<dia.AddDays(1));
  if(profissionalId.HasValue)q=q.Where(c=>c.MedicoId==profissionalId);
  var consultas=await q.OrderByDescending(c=>c.Data).ThenBy(c=>c.Horario).Take(501).ToListAsync(ct);var limite=consultas.Count>500;consultas=consultas.Take(500).ToList();
  var ids=consultas.Select(c=>c.Id).ToArray();var js=await db.Jornadas.AsNoTracking().Where(j=>ids.Contains(j.ConsultaId)).ToDictionaryAsync(j=>j.ConsultaId,ct);
  var sessoes=await db.SessoesTele.AsNoTracking().Where(s=>ids.Contains(s.ConsultaId)).OrderByDescending(s=>s.EntradaEm).Take(3000).ToListAsync(ct);var agora=presenca.Agora;
  var impedimentos=new HashSet<int>();
  foreach(var c in consultas.Where(c=>js.GetValueOrDefault(c.Id)?.Tele==true&&!JornadaService.Encerrada(c)))if(await new MKSANCrud.Services.Psicologia.PsicologiaService(db,clock).ImpedimentoAsync(c,ct)!=null)impedimentos.Add(c.Id);
  ConsultaSalaLinha Linha(Consulta c) {
   var j=js.GetValueOrDefault(c.Id);var historico=sessoes.Where(s=>s.ConsultaId==c.Id).Take(20).ToList();
   var atuais=sessoes.Where(s=>s.ConsultaId==c.Id&&TeleconsultaPresencaService.Presente(s,c,agora)).ToList();
   var pp=atuais.Any(s=>s.Papel=="Paciente");var mp=atuais.Any(s=>s.Papel=="Profissional");var inicio=SalasService.Inicio(c);
   var estado=JornadaService.Encerrada(c)||j?.FimEm!=null?c.Status:j?.Tele!=true?JornadaService.Etapa(j):inicio==null?"Revisar horário":
    clock.Agora>inicio.Value.AddHours(4)?"Período encerrado":impedimentos.Contains(c.Id)?"Revisar preparação":j.InicioEm!=null?"Em atendimento":
    pp&&mp?atuais.Where(s=>s.Estado=="conectada").Select(s=>s.Papel).Distinct().Count()==2?"Chamada conectada":"Conectando participantes":
    pp?"Paciente aguardando":mp?"Profissional aguardando":clock.Agora<inicio.Value.AddMinutes(-30)?"Agendada":
    j.ConsentimentoEm==null||j.ConsentimentoVersao!=ConsentimentoAtendimento.Versao(c)?"Aguardando autorização":"Sala aberta";
   var conexao=!pp&&!mp?"Sem presença recente":atuais.Any(s=>s.Estado=="interrompida")?"Interrompida":pp&&mp?atuais.Where(s=>s.Estado=="conectada").Select(s=>s.Papel).Distinct().Count()==2?"Conectada":"Conectando":"Aguardando outro participante";
   return new(c,j,estado,pp,mp,historico,conexao);
  }
  var linhas=consultas.Select(Linha).ToList();bool NoDia(ConsultaSalaLinha l)=>l.Consulta.Data.Date==dia||SalasService.Inicio(l.Consulta)?.AddMinutes(l.Jornada?.Duracao??l.Consulta.DuracaoMinutos)>dia;
  var blocos=await db.BloqueiosSala.AsNoTracking().Where(b=>b.CanceladoEm==null&&b.Inicio<dia.AddDays(1)&&b.Fim>dia).OrderBy(b=>b.Inicio).ToListAsync(ct);
  var ocupacoes=equipe?await db.Jornadas.AsNoTracking().Include(x=>x.Consulta).Where(x=>x.SalaId!=null&&x.Consulta.Data>=clock.Hoje.AddDays(-1)&&x.Consulta.Data<clock.Hoje.AddDays(1)&&x.FimEm==null&&x.Consulta.Status!=ConsultaStatus.Cancelada&&x.Consulta.Status!=ConsultaStatus.Ausente).ToListAsync(ct):[];
  var fisicas=await db.Salas.AsNoTracking().OrderByDescending(s=>s.Ativa).ThenBy(s=>s.Nome).ToListAsync(ct);
  var sl=fisicas.Select(s=>{
   var reservas=linhas.Where(l=>l.Jornada?.SalaId==s.Id&&NoDia(l)).ToList();var bloqueios=blocos.Where(b=>b.SalaId==s.Id).ToList();
   var ocupada=reservas.Any(l=>SalasService.Ocupa(l.Consulta)&&l.Jornada?.FimEm==null&&SalasService.Inicio(l.Consulta)<=clock.Agora&&SalasService.Inicio(l.Consulta)?.AddMinutes(l.Jornada?.Duracao??l.Consulta.DuracaoMinutos)>clock.Agora);
   if(equipe)ocupada=ocupacoes.Any(x=>x.SalaId==s.Id&&SalasService.Inicio(x.Consulta)<=clock.Agora&&(x.InicioEm!=null||SalasService.Inicio(x.Consulta)?.AddMinutes(x.Duracao)>clock.Agora));
   var estado=!s.Ativa?"Inativa":dia!=clock.Hoje?"Agenda do dia":bloqueios.Any(b=>b.Inicio<=clock.Agora&&b.Fim>clock.Agora)?"Bloqueada":ocupada?"Ocupada":equipe?"Livre agora":"Minhas reservas";
   return new SalaFisicaLinha(s,estado,reservas,bloqueios);
  }).ToList();
  var profissionais=equipe?await db.Profissionais.AsNoTracking().Where(p=>p.Ativo).OrderBy(p=>p.Nome).ToListAsync(ct):[];
  return View(new SalasPainel(dia,aba,equipe,sl,linhas.Where(l=>l.Jornada?.Tele==true&&l.Consulta.Data.Date==dia).ToList(),linhas.Where(l=>l.Jornada?.Tele!=true&&l.Jornada?.SalaId==null&&l.Consulta.Data.Date==dia&&!JornadaService.Encerrada(l.Consulta)).ToList(),profissionais,profissionalId,limite));
 }
 IActionResult Resultado(string? erro,DateTime? data,string sucesso){TempData[erro==null?"Sucesso":"Erro"]=erro??sucesso;return RedirectToAction(nameof(Index),new{data=data?.ToString("yyyy-MM-dd"),aba="fisicas"});}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> Salvar(int? id,string nome,string? localizacao,string? recursos,bool ativa,DateTime? data,CancellationToken ct){if(!JornadaService.Equipe(User))return Forbid();if(!ModelState.IsValid)return Resultado("Confira os campos da sala.",data,"");return Resultado(await salas.SalvarAsync(id,nome,localizacao,recursos,ativa,User,ct),data,"Sala salva.");}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> Bloquear(int salaId,DateTime inicio,DateTime fim,string motivo,DateTime? data,CancellationToken ct){if(!JornadaService.Equipe(User))return Forbid();if(!ModelState.IsValid)return Resultado("Confira as datas do bloqueio.",data,"");return Resultado(await salas.BloquearAsync(salaId,inicio,fim,motivo,User,ct),data,"Bloqueio registrado.");}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> Liberar(int id,DateTime? data,CancellationToken ct){if(!JornadaService.Equipe(User))return Forbid();return Resultado(await salas.LiberarAsync(id,User,ct),data,"Bloqueio liberado.");}
}
