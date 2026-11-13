using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Controllers;
[Authorize,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PsicologiaController(MKSANContext db,JornadaService jornada,IClinicaClock clock):Controller {
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> VerificarRegistro(int id,string fonte,bool confirmar,bool responsavelTecnico,CancellationToken ct) {
  var p=await db.Psicologos.FindAsync([id],ct);if(p==null)return NotFound();
  if(!confirmar||!RegistroProfissional.Valido(p)||string.IsNullOrWhiteSpace(fonte)||fonte.Length>500){TempData["Erro"]="Confira o registro no conselho oficial e informe a referência da verificação.";}
  else {p.ResponsavelTecnico=p.EhPsicologia&&responsavelTecnico;p.RegistroVerificadoEm=DateTime.UtcNow;p.RegistroVerificadoPor=User.FindFirstValue(ClaimTypes.NameIdentifier);p.FonteVerificacaoRegistro=fonte.Trim();jornada.Audit(User,"Verificar registro profissional",id);await db.SaveChangesAsync(ct);TempData["Sucesso"]="Verificação do registro profissional documentada.";}
  return RedirectToAction("Details","Medico",new{id});
 }
 [Authorize(Roles="Psicologo,Medico"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> Responsavel(int id,string nome,string vinculo,string referencia,int dias,bool confirmar,CancellationToken ct) {
  var c=await jornada.Visiveis(User).SingleOrDefaultAsync(c=>c.Id==id,ct);if(c==null)return NotFound();
  if(!JornadaService.Medico(User,c)||c.Medico?.HabilitadoClinicamente!=true)return Forbid();
  if(!confirmar||!PsicologiaService.Menor(c.Paciente!,clock.Hoje)||string.IsNullOrWhiteSpace(nome)||nome.Length>150||string.IsNullOrWhiteSpace(vinculo)||vinculo.Length>80||string.IsNullOrWhiteSpace(referencia)||referencia.Length>300||dias is <1 or >365){TempData["Erro"]="Confira os dados e a autorização do responsável. A referência deve identificar o documento guardado pela clínica, sem expor seu conteúdo.";}
  else{db.AutorizacoesResponsaveis.Add(new(){PacienteId=c.PacienteId,PsicologoId=c.MedicoId,Nome=nome.Trim(),Vinculo=vinculo.Trim(),Referencia=referencia.Trim(),VerificadaPor=User.FindFirstValue(ClaimTypes.NameIdentifier)!,ValidaAte=DateTime.UtcNow.AddDays(dias)});jornada.Audit(User,"Conferir autorização de responsável",id);await db.SaveChangesAsync(ct);TempData["Sucesso"]="Autorização conferida e registrada.";}
  return RedirectToAction("Consulta","Jornada",new{id});
 }
 [Authorize(Roles="Psicologo,Medico"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> RevogarResponsavel(Guid id,int consultaId,CancellationToken ct){var c=await jornada.Visiveis(User).SingleOrDefaultAsync(x=>x.Id==consultaId,ct);if(c==null||!JornadaService.Medico(User,c))return NotFound();var a=await db.AutorizacoesResponsaveis.SingleOrDefaultAsync(x=>x.Id==id&&x.PacienteId==c.PacienteId&&x.PsicologoId==c.MedicoId,ct);if(a==null)return NotFound();a.RevogadaEm??=DateTime.UtcNow;jornada.Audit(User,"Revogar autorização de responsável",consultaId);await db.SaveChangesAsync(ct);return RedirectToAction("Consulta","Jornada",new{id=consultaId});}
 [Authorize(Roles="Admin"),HttpGet]
 public async Task<IActionResult> Pep(CancellationToken ct){var adapter=HttpContext.RequestServices.GetRequiredService<FhirPsicologiaService>();ViewBag.Habilitado=adapter.Habilitado;ViewBag.Configurado=adapter.Configurado;ViewBag.Pendentes=await db.PepEventos.CountAsync(x=>x.FhirEntregueEm==null,ct);ViewBag.Esgotadas=await db.PepEventos.CountAsync(x=>x.FhirEntregueEm==null&&x.FhirTentativas>=10,ct);return View();}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> TestarPep(CancellationToken ct){TempData["Sucesso"]=await HttpContext.RequestServices.GetRequiredService<FhirPsicologiaService>().TestarAsync(ct);jornada.Audit(User,"Testar capacidades administrativas FHIR",0);await db.SaveChangesAsync(ct);return RedirectToAction(nameof(Pep));}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> ExportarPep(int id,CancellationToken ct){var adapter=HttpContext.RequestServices.GetRequiredService<FhirPsicologiaService>();if(!adapter.Configurado){TempData["Erro"]="Configure o adaptador FHIR antes de exportar.";return RedirectToAction(nameof(Pep));}var bundle=await adapter.BundleAsync(id,ct);if(bundle==null)return NotFound();jornada.Audit(User,"Baixar transação administrativa FHIR",id);await db.SaveChangesAsync(ct);return File(System.Text.Encoding.UTF8.GetBytes(bundle),"application/fhir+json","CallMed-sessao-"+id+"-fhir.json");}
 [Authorize(Roles="Psicologo,Medico"),HttpGet]
 public async Task<IActionResult> Acervo(CancellationToken ct){if(!await Tecnico(ct))return Forbid();return View();}
 Task<bool> Tecnico(CancellationToken ct){var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);return db.Psicologos.AnyAsync(p=>p.UsuarioId==uid&&p.Ativo&&p.ResponsavelTecnico&&(p.AreaProfissional==""||p.AreaProfissional=="Psicologia")&&p.Crp!=null&&p.RegistroVerificadoEm!=null,ct);}
 [Authorize(Roles="Psicologo,Medico"),HttpGet]
 public async Task<IActionResult> AcervoChaves(CancellationToken ct){if(!await Tecnico(ct))return Forbid();var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);return Json(new{usuarioId=uid,medicoId=uid,pacienteId=(string?)null,formulario=(object?)null,chaves=await db.ChavesClinicas.Where(k=>k.UsuarioId==uid&&k.RevogadaEm==null).ToListAsync(ct),revogadas=await db.ChavesClinicas.Where(k=>k.UsuarioId==uid&&k.RevogadaEm!=null).Select(k=>k.Id).ToListAsync(ct)});}
 [Authorize(Roles="Psicologo,Medico"),HttpGet]
 public async Task<IActionResult> AcervoDados(CancellationToken ct){if(!await Tecnico(ct))return Forbid();var uid=User.FindFirstValue(ClaimTypes.NameIdentifier);var rtId=await db.Psicologos.Where(p=>p.UsuarioId==uid).Select(p=>p.Id).SingleAsync(ct);var rows=await db.GuardasRegistrosPsicologicos.AsNoTracking().Where(g=>g.PsicologoId==rtId).OrderByDescending(g=>g.ConcedidaEm).Take(1000).ToListAsync(ct);var ids=rows.Select(g=>g.RegistroId).Distinct().ToArray();var acervo=await db.RegistrosPsicologicos.AsNoTracking().Where(r=>ids.Contains(r.Id)).ToDictionaryAsync(r=>r.Id,ct);var registros=new List<object>();foreach(var group in rows.GroupBy(g=>g.RegistroId)){if(!acervo.TryGetValue(group.Key,out var registro))continue;var envelope=System.Text.Json.Nodes.JsonNode.Parse(registro.Envelope)!;var recipients=envelope["destinatarios"]!.AsArray();foreach(var g in group)if(!recipients.Any(n=>n!["id"]!.GetValue<Guid>()==g.ChaveId))recipients.Add(new System.Text.Json.Nodes.JsonObject{["id"]=g.ChaveId.ToString(),["key"]=g.ChaveCifrada});registros.Add(new{registro.Id,registro.ConsultaId,registro.Categoria,registro.Escopo,registro.RetificaId,registro.CriadoEm,envelope=envelope.ToJsonString()});}jornada.Audit(User,"Consultar acervo sob guarda técnica",0);await db.SaveChangesAsync(ct);return Json(new{podePublicar=false,registros});}
 [Authorize(Roles="Admin"),HttpPost,ValidateAntiForgeryToken]
 public async Task<IActionResult> RetomarPep(bool confirmar,CancellationToken ct){if(!confirmar){TempData["Erro"]="Confirme a retomada após corrigir a integração com o fornecedor.";return RedirectToAction(nameof(Pep));}var rows=await db.PepEventos.Where(x=>x.FhirEntregueEm==null&&x.FhirTentativas>=10).Take(1000).ToListAsync(ct);foreach(var e in rows){e.FhirTentativas=0;e.FhirTentarEm=DateTime.UtcNow;}jornada.Audit(User,"Retomar exportações administrativas FHIR",0);await db.SaveChangesAsync(ct);TempData["Sucesso"]="Pendências recolocadas na fila. A entrega depende da configuração ativa e da resposta do fornecedor.";return RedirectToAction(nameof(Pep));}
}
