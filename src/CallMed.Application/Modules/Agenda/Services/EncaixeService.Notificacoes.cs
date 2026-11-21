using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;
namespace MKSANCrud.Services.Clinica;
public sealed partial class EncaixeService
{

 public async Task<int> ProcessarAsync(CancellationToken ct) {
  await ReservarAsync(ct);var now=DateTime.UtcNow;var offers=await db.Ofertas.Include(x=>x.ListaEspera).ThenInclude(x=>x.Paciente).Where(x=>x.Estado=="Aberta"&&x.ExpiraEm>now&&x.NotificadaEm==null&&x.Tentativas<5).OrderBy(x=>x.CriadoEm).Take(30).ToListAsync(ct);var n=0;
  foreach(var o in offers){var i=o.ListaEspera;var p=i.Paciente;if(p?.Ativo!=true||!i.Ativa){o.Estado="Revogada";continue;}o.Tentativas++;await db.SaveChangesAsync(ct);var dest=await MKSANCrud.Services.Usuarios.ContaAvisos.EscolherAsync(db,p,envio,ct);
   if(dest==null){var marker=$"Oferta de encaixe {o.Id}: {o.Data:dd/MM/yyyy} às {o.Horario}. Reserva até {clock.ConverterUtc(o.ExpiraEm):HH:mm}.";if(!await db.SolicitacoesAtendimento.AnyAsync(x=>x.PacienteId==p.Id&&x.Observacao==marker,ct))db.SolicitacoesAtendimento.Add(new(){PacienteId=p.Id,MedicoId=o.MedicoId,Canal=CanalAtendimento.Telefone,Status=StatusSolicitacaoAtendimento.Nova,NomeContato=p.Nome,TelefoneContato=p.Telefone,DataPreferida=o.Data,PeriodoPreferido=i.Periodo,Observacao=marker});o.NotificadaEm=now;continue;}
   var c=await conversas.ObterOuCriarAsync(dest.Value.Canal,dest.Value.Identificador,p.Id,"Encaixe CallMed",ct:ct);var origin=(config["RENDER_EXTERNAL_URL"]??config["Twa:Origin"]??"").TrimEnd('/');var portal=Uri.TryCreate(origin,UriKind.Absolute,out var u)&&u.Scheme=="https"?origin+"/Jornada#ofertas":"sua conta CallMed";
   var text=$"CallMed: uma vaga está reservada para você em {o.Data:dd/MM} às {o.Horario}. Acesse {portal} para aceitar ou recusar antes das {clock.ConverterUtc(o.ExpiraEm):HH:mm}. O agendamento só ocorre após o aceite.";var msg=await envio.EnviarAvisoAsync(c,text,"encaixe:"+o.Id,ct);if(msg.Status is StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue){o.NotificadaEm=DateTime.UtcNow;i.NotificadoEm=o.NotificadaEm;n++;}
  }await db.SaveChangesAsync(ct);return n;
 }
}
