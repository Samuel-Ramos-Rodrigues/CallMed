using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public sealed class JornadaService(MKSANContext db,IClinicaClock clock) {
 public static bool Encerrada(Consulta c)=>c.Status is ConsultaStatus.Cancelada or ConsultaStatus.Realizada or ConsultaStatus.Ausente;
 public static string Snapshot(Consulta c)=>$"{c.PacienteId}|{c.MedicoId}|{c.Data:yyyyMMdd}|{c.Horario}|{c.Revisao}";
 public static string Etapa(Jornada? j)=>j?.FimEm!=null?"Concluído":j?.InicioEm!=null?"Em atendimento":j?.ChegadaEm!=null?"Aguardando atendimento":j?.CheckInEm!=null?"Check-in antecipado":j?.PresencaConfirmadaEm!=null?"Presença confirmada":"Preparação";
 public static bool Equipe(ClaimsPrincipal u)=>!u.HasClaim(x=>x.Type=="clinical.consulta")&&(u.IsInRole("Admin")||u.IsInRole("Funcionario"));
 public static bool Paciente(ClaimsPrincipal u,Consulta c)=>c.Paciente?.UsuarioId==u.FindFirstValue(ClaimTypes.NameIdentifier);
 public static bool Medico(ClaimsPrincipal u,Consulta c)=>u.IsProfissionalSaude()&&c.Medico?.UsuarioId==u.FindFirstValue(ClaimTypes.NameIdentifier);
 public IQueryable<Consulta> Visiveis(ClaimsPrincipal u) {
  var uid=u.FindFirstValue(ClaimTypes.NameIdentifier);var q=db.Consultas.Include(c=>c.Paciente).Include(c=>c.Medico).AsQueryable();
  if(u.HasClaim(x=>x.Type=="clinical.consulta")){var id=int.Parse(u.FindFirstValue("clinical.consulta")!);return q.Where(c=>c.Id==id&&c.Paciente!.UsuarioId==uid);}
  if(Equipe(u))return q;return u.IsProfissionalSaude()?q.Where(c=>c.Medico!.UsuarioId==uid):q.Where(c=>c.Paciente!.UsuarioId==uid);
 }
 public async Task LockAsync(string key,CancellationToken ct) {if(db.Database.IsRelational())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0));",ct);}
 public void Audit(ClaimsPrincipal u,string acao,int id)=>db.AuditoriaEventos.Add(new(){Acao=acao,Entidade="Jornada",EntidadeId=id.ToString(),UsuarioId=u.FindFirstValue(ClaimTypes.NameIdentifier),CriadoEm=DateTime.UtcNow});
 public async Task<Jornada> ObterAsync(int id,CancellationToken ct) {
  var j=await db.Jornadas.Include(x=>x.Sala).SingleOrDefaultAsync(x=>x.ConsultaId==id,ct);if(j!=null)return j;
  var duracao=await db.Consultas.Where(c=>c.Id==id).Select(c=>c.DuracaoMinutos).FirstAsync(ct);j=new(){ConsultaId=id,Duracao=Math.Clamp(duracao,5,240)};db.Jornadas.Add(j);await db.SaveChangesAsync(ct);return j;
 }
 public async Task<string?> MoverAsync(Consulta c,ClaimsPrincipal u,string acao,bool confirmar,CancellationToken ct) {
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await LockAsync("jornada:"+c.Id,ct);if(db.Database.IsRelational())await db.Entry(c).ReloadAsync(ct);
  if(Encerrada(c))return "A consulta já foi encerrada.";var j=await ObterAsync(c.Id,ct);await db.Entry(j).ReloadAsync(ct);var agora=DateTime.UtcNow;
  switch(acao) {
   case "confirmar":if(!Paciente(u,c))return "Ação exclusiva do paciente.";j.PresencaConfirmadaEm??=agora;break;
   case "cadastro":if(!Paciente(u,c)||!confirmar)return "Confira e confirme os dados.";if(string.IsNullOrWhiteSpace(c.Paciente?.Telefone)||c.Paciente.DataNascimento==null)return "Complete telefone e nascimento em Minha conta.";j.CadastroEm=agora;break;
   case "checkin":if(!Paciente(u,c))return "Ação exclusiva do paciente.";var quando=clock.ConverterParaUtc(c.Data.Date.Add(TimeSpan.Parse(c.Horario)));if(agora<quando.AddHours(-24)||agora>quando.AddHours(4))return "Check-in disponível de 24 horas antes a 4 horas após a consulta.";if(j.CadastroEm==null)return "Confira o pré-cadastro primeiro.";j.CheckInEm??=agora;break;
   case "chegada":if(!Equipe(u)||c.Data.Date!=clock.Hoje)return "A recepção deve validar a presença no dia da consulta.";j.ChegadaEm??=agora;break;
   case "pagamento":if(!Equipe(u))return "A recepção deve conferir o pagamento ou convênio.";j.PagamentoConferido=confirmar;break;
   case "iniciar":
    if(!Medico(u,c))return "Somente o profissional responsável pode iniciar o atendimento.";
    var impedimento=await new PsicologiaService(db,clock).ImpedimentoAsync(c,ct);if(impedimento!=null)return impedimento;
    if(j.Tele){
     if(!confirmar)return "Confira a identidade do paciente na chamada antes de iniciar.";
     if(j.ConsentimentoEm==null||j.ConsentimentoVersao!=ConsentimentoAtendimento.Versao(c))return "Aguarde a autorização do paciente para a consulta online.";
     if(!TimeSpan.TryParse(c.Horario,System.Globalization.CultureInfo.InvariantCulture,out var horarioOnline)||horarioOnline<TimeSpan.Zero||horarioOnline>=TimeSpan.FromDays(1))return "Confira o horário com a clínica antes de iniciar.";
     var inicioOnline=c.Data.Date.Add(horarioOnline);
     if(clock.Agora<inicioOnline.AddMinutes(-30)||clock.Agora>inicioOnline.AddHours(4))return "O atendimento online deve começar dentro do período de acesso à sala.";
    }else if(j.ChegadaEm==null||c.Data.Date!=clock.Hoje)return "O profissional responsável inicia após a recepção validar a presença.";
    await LockAsync("atendimento-medico:"+c.MedicoId,ct);
    if(await db.Jornadas.AnyAsync(x=>x.ConsultaId!=c.Id&&x.Consulta.MedicoId==c.MedicoId&&x.InicioEm!=null&&x.FimEm==null&&x.Consulta.Status!=ConsultaStatus.Cancelada,ct))return "Conclua o atendimento atual antes de iniciar outro.";
    if(j.Tele)j.ChegadaEm??=agora;
    j.InicioEm??=agora;break;
   case "concluir":if(!Medico(u,c)||j.InicioEm==null)return "Inicie o atendimento antes de concluir.";j.FimEm??=agora;c.Status=ConsultaStatus.Realizada;c.RealizadaEm=agora;break;
   case "consentir":if(!Paciente(u,c)||!confirmar||!j.Tele)return "Leia e aceite o consentimento da consulta online.";j.ConsentimentoEm=agora;j.ConsentimentoVersao=ConsentimentoAtendimento.Versao(c);
    db.AuditoriaEventos.Add(new(){Acao="Aceitar consulta online",Entidade="Jornada",EntidadeId=c.Id.ToString(),UsuarioId=u.FindFirstValue(ClaimTypes.NameIdentifier),CriadoEm=agora,ValorNovo=System.Text.Json.JsonSerializer.Serialize(new{versao=ConsentimentoAtendimento.Versao(c),texto=ConsentimentoAtendimento.Texto(c),hash=ConsentimentoAtendimento.Hash(c),snapshot=Snapshot(c),crp=c.Medico?.Crp})});break;
   default:return "Ação inválida.";
  }
  j.AtualizadoEm=agora;Audit(u,acao,c.Id);await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return null;
 }
 // Chegada por autoatendimento: o controller verifica um QR temporario assinado
 // e o servico preserva as regras clinicas, identidade e idempotencia.
 public async Task<string?> RegistrarChegadaAutonomaAsync(Consulta c,ClaimsPrincipal usuario,CancellationToken ct) {
  if(!Paciente(usuario,c))return "A consulta não pertence a esta conta.";
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
  await LockAsync("jornada:"+c.Id,ct);
  if(db.Database.IsRelational())await db.Entry(c).ReloadAsync(ct);
  if(Encerrada(c)||c.Data.Date!=clock.Hoje)return "Esta consulta não está disponível para chegada hoje.";
  if(!TimeSpan.TryParse(c.Horario,System.Globalization.CultureInfo.InvariantCulture,out var horario))return "O horário precisa ser revisado.";
  var inicio=c.Data.Date.Add(horario);
  if(clock.Agora<inicio.AddHours(-2)||clock.Agora>inicio.AddHours(4))return "O check-in presencial está disponível de duas horas antes até quatro horas após o horário da consulta.";
  var j=await ObterAsync(c.Id,ct);
  await db.Entry(j).ReloadAsync(ct);
  if(j.Tele)return "Para teleconsulta, entre na sala online da consulta.";
  if(j.CadastroEm==null)return "Confira seu pré-cadastro antes de registrar a chegada.";
  if(j.ChegadaEm.HasValue){if(tx!=null)await tx.CommitAsync(ct);return null;}
  var agora=DateTime.UtcNow;
  j.CheckInEm??=agora;
  j.ChegadaEm=agora;
  j.AtualizadoEm=agora;
  Audit(usuario,"Chegada presencial por QR temporário",c.Id);
  await db.SaveChangesAsync(ct);
  if(tx!=null)await tx.CommitAsync(ct);
  return null;
 }
 public async Task<string?> AlocarAsync(Consulta c,ClaimsPrincipal u,int? salaId,int duracao,bool tele,CancellationToken ct) {
  if(!Equipe(u))return "Somente a recepção pode organizar as salas.";if(duracao<5||duracao>240)return "Escolha duração entre 5 e 240 minutos.";
  await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;await LockAsync("jornada:"+c.Id,ct);if(db.Database.IsRelational())await db.Entry(c).ReloadAsync(ct);var j=await ObterAsync(c.Id,ct);await db.Entry(j).ReloadAsync(ct);
  await LockAsync($"mksan-dia:{c.MedicoId}:{c.Data:yyyy-MM-dd}",ct);
  var agendas=await db.Consultas.AsNoTracking().Where(x=>x.MedicoId==c.MedicoId&&x.Id!=c.Id&&x.Data==c.Data&&x.Status!=ConsultaStatus.Cancelada).Select(x=>new{x.Horario,Duracao=db.Jornadas.Where(y=>y.ConsultaId==x.Id).Select(y=>(int?)y.Duracao).FirstOrDefault()??x.DuracaoMinutos}).ToListAsync(ct);
  if(agendas.Any(x=>MKSANCrud.Services.Agendamento.AgendamentoService.Sobrepoe(c.Horario,duracao,x.Horario,x.Duracao)))return "A duração invade o horário de outra sessão do profissional.";
  if(Encerrada(c)||j.InicioEm!=null)return "Não altere atendimentos iniciados ou encerrados.";if(tele)salaId=null;
  if(salaId.HasValue){await LockAsync("sala:"+salaId,ct);var salaInicio=SalasService.Inicio(c);if(salaInicio==null)return "Confira o horário antes de reservar a sala.";var salaFim=salaInicio.Value.AddMinutes(duracao);if(await db.BloqueiosSala.AnyAsync(x=>x.SalaId==salaId&&x.CanceladoEm==null&&x.Inicio<salaFim&&x.Fim>salaInicio.Value,ct))return "A sala está bloqueada em parte desse horário.";if(!await db.Salas.AnyAsync(x=>x.Id==salaId&&x.Ativa,ct))return "Sala indisponível.";var outros=await db.Jornadas.Include(x=>x.Consulta).Where(x=>x.SalaId==salaId&&x.ConsultaId!=c.Id&&x.Consulta.Data>=c.Data.Date.AddDays(-1)&&x.Consulta.Data<=c.Data.Date.AddDays(1)&&x.Consulta.Status!=ConsultaStatus.Cancelada&&x.Consulta.Status!=ConsultaStatus.Ausente).ToListAsync(ct);if(outros.Any(x=>SalasService.Inicio(x.Consulta) is DateTime at&&at<salaFim&&at.AddMinutes(x.Duracao)>salaInicio))return "A sala já está reservada em parte desse horário.";}
  if(j.Tele!=tele){j.ConsentimentoEm=null;j.ConsentimentoVersao=null;}c.DuracaoMinutos=duracao;j.SalaId=salaId;j.Duracao=duracao;j.Tele=tele;j.AtualizadoEm=DateTime.UtcNow;Audit(u,"Organizar sala",c.Id);await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return null;
 }
 public async Task<object> IndicadoresAsync(CancellationToken ct) {
  var inicio=DateTime.UtcNow.AddDays(-30);var js=await db.Jornadas.AsNoTracking().Include(x=>x.Consulta).Where(x=>x.AtualizadoEm>=inicio).ToListAsync(ct);var tempos=js.Where(x=>!x.Tele&&x.InicioEm!=null&&x.ChegadaEm!=null&&x.InicioEm>=x.ChegadaEm).Select(x=>(x.InicioEm!.Value-x.ChegadaEm!.Value).TotalMinutes).ToArray();
  return new{janela="Últimos 30 dias",confirmacoesAtivas=js.Count(x=>x.PresencaConfirmadaEm!=null),tempoMedioConfirmacaoMinutos=js.Any(x=>x.PresencaConfirmadaEm>=x.Consulta.CriadoEm)?Math.Round(js.Where(x=>x.PresencaConfirmadaEm>=x.Consulta.CriadoEm).Average(x=>(x.PresencaConfirmadaEm!.Value-x.Consulta.CriadoEm).TotalMinutes),1):(double?)null,checkins=js.Count(x=>x.CheckInEm!=null),chegadas=js.Count(x=>x.ChegadaEm!=null),concluidos=js.Count(x=>x.FimEm!=null),amostra=tempos.Length,esperaMedia=tempos.Length==0?(double?)null:Math.Round(tempos.Average(),1),ofertas=await db.Ofertas.CountAsync(x=>x.CriadoEm>=inicio,ct),aceites=await db.Ofertas.CountAsync(x=>x.CriadoEm>=inicio&&x.Estado=="Aceita",ct),medicoes=await db.MedicoesOperacionais.AsNoTracking().OrderBy(x=>x.Data).Take(200).ToListAsync(ct)};
 }
}
