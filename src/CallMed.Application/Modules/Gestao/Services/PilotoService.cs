using MKSANCrud.Application.Binding;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;

public sealed class MedicaoPilotoInput {
 public DateTime Data {get;set;}
 public string Periodo {get;set;} = "Piloto";
 public int RevisaoAnterior {get;set;}
 public string? AgendaHash {get;set;}
 [Range(0,100000)] public int? Realizadas {get;set;}
 [Range(0,100000)] public int? Faltas {get;set;}
 [Range(0,100000)] public int? Canceladas {get;set;}
 [Range(0,100000)] public int? SemDesfecho {get;set;}
 [Range(0,144000)] public int? MinutosAgendados {get;set;}
 [Range(0,144000)] public int? MinutosDisponiveis {get;set;}
 [Range(0,144000)] public int? MinutosRecepcao {get;set;}
 [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(MKSANCrud.Application.Binding.PilotoCustoBinder))]
 [Range(typeof(decimal),"0","10000")] public decimal? CustoHoraRecepcao {get;set;}
 [Required,StringLength(300)] public string Fonte {get;set;} = "";
 [Required,StringLength(300)] public string Motivo {get;set;} = "";
 public bool Conferido {get;set;}
 public bool Desconsiderar {get;set;}
}
public sealed record AgendaPiloto(int Realizadas,int Faltas,int Canceladas,int SemDesfecho,int MinutosAgendados,string Hash);
public sealed class MedicaoConflitoException(string mensagem):Exception(mensagem);
public sealed record PilotoPeriodo(DateTime Inicio,DateTime Fim,int DiasEsperados,int DiasMedidos,int Realizadas,int Faltas,int Canceladas,int SemDesfecho,
 double? TaxaFaltas,int DiasOcupacao,long MinutosAgendados,long MinutosDisponiveis,double? Ocupacao,
 int DiasRecepcao,long MinutosRecepcao,long ConsultasRecepcao,decimal? MinutosPorConsulta,
 int DiasCusto,decimal? CustoRecepcao,long ConsultasCusto,decimal? CustoPorConsulta);
public sealed record ComparacaoPiloto(PilotoPeriodo Antes,PilotoPeriodo Piloto,double? DiferencaFaltasPp,double? DiferencaOcupacaoPp,
 decimal? DiferencaMinutosPorConsulta,decimal? DiferencaCustoPorConsulta);

public sealed record RelatorioPiloto(ComparacaoPiloto Comparacao,MedicaoPiloto[] Medicoes);

public sealed class PilotoService(MKSANContext db,IClinicaClock clock) {
 public void ValidarDia(DateTime dia,string periodo) {
  if(periodo is not ("Antes" or "Piloto")||dia.Date<new DateTime(2000,1,1)||dia.Date>=clock.Hoje)
   throw new ArgumentException("Escolha Antes ou Piloto e um dia encerrado, entre 01/01/2000 e ontem.");
 }
 public async Task<AgendaPiloto> AgendaAsync(DateTime dia,CancellationToken ct=default) {
  ValidarDia(dia,"Piloto");dia=dia.Date;
  // Uma consulta ao banco captura estado e duração da mesma leitura. Sem conteúdo clínico.
  var linhas=await (from c in db.Consultas.AsNoTracking() join j in db.Jornadas.AsNoTracking() on c.Id equals j.ConsultaId into jornadas
    from j in jornadas.DefaultIfEmpty() where c.Data>=dia&&c.Data<dia.AddDays(1)
    orderby c.Id select new {c.Id,c.Status,Duracao=j==null?c.DuracaoMinutos:j.Duracao}).ToListAsync(ct);
  if(linhas.Any(x=>x.Duracao<=0||x.Duracao>1440))throw new ArgumentException("Há uma duração de consulta inválida. Revise a agenda antes de medir.");
  var minutos=linhas.Where(x=>x.Status!=ConsultaStatus.Cancelada).Sum(x=>x.Duracao);
  if(minutos>144000)throw new ArgumentException("O volume diário excede o limite desta medição.");
  var canonical=dia.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"\n"+string.Join("\n",linhas.Select(x=>FormattableString.Invariant($"{x.Id}|{x.Status}|{x.Duracao}")));
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
  return new(linhas.Count(x=>x.Status==ConsultaStatus.Realizada),linhas.Count(x=>x.Status==ConsultaStatus.Ausente),linhas.Count(x=>x.Status==ConsultaStatus.Cancelada),
    linhas.Count(x=>x.Status!=ConsultaStatus.Realizada&&x.Status!=ConsultaStatus.Ausente&&x.Status!=ConsultaStatus.Cancelada),minutos,hash);
 }
 public Task<MedicaoPiloto?> UltimaAsync(DateTime data,string periodo,CancellationToken ct=default)=>db.MedicoesPiloto.AsNoTracking()
  .Where(x=>x.Data==data.Date&&x.Periodo==periodo).OrderByDescending(x=>x.Revisao).FirstOrDefaultAsync(ct);
 public async Task<MedicaoPiloto> RegistrarAsync(MedicaoPilotoInput input,string usuario,CancellationToken ct=default) {
  ValidarDia(input.Data,input.Periodo);
  var erros=new List<ValidationResult>();
  if(!Validator.TryValidateObject(input,new ValidationContext(input),erros,true)||!input.Conferido||string.IsNullOrWhiteSpace(input.Fonte)||string.IsNullOrWhiteSpace(input.Motivo)||string.IsNullOrWhiteSpace(usuario)||(input.CustoHoraRecepcao is decimal custo&&Math.Round(custo,2)!=custo))
   throw new ArgumentException("Preencha a fonte, o motivo, os valores válidos e confirme a conferência dos dados.");
  var anterior=await UltimaAsync(input.Data,input.Periodo,ct);
  if(input.RevisaoAnterior!=(anterior?.Revisao??0))throw new MedicaoConflitoException("Outra pessoa registrou este dia. Recarregue a medição e confira a revisão mais recente.");
  AgendaPiloto? agenda=null;
  if(input.Desconsiderar&&anterior==null)throw new ArgumentException("Só é possível desconsiderar um dia já registrado. Informe o motivo da correção.");
  if(!input.Desconsiderar&&input.Periodo=="Piloto") {
   agenda=await AgendaAsync(input.Data,ct);
   if(input.AgendaHash!=agenda.Hash)throw new MedicaoConflitoException("A agenda mudou desde a prévia. Recarregue a medição e confira os novos totais.");
  } else if(!input.Desconsiderar&&(input.Realizadas==null||input.Faltas==null||input.Canceladas==null||input.SemDesfecho==null))
   throw new ArgumentException("Na linha de base, informe separadamente realizadas, faltas, canceladas e consultas sem desfecho.");
  if(input.Desconsiderar){var retirada=new MedicaoPiloto{Data=input.Data.Date,Periodo=input.Periodo,Revisao=anterior!.Revisao+1,Desconsiderada=true,Fonte=input.Fonte.Trim(),Motivo=input.Motivo.Trim(),RegistradoEm=DateTime.UtcNow,RegistradoPor=usuario};return await PersistirAsync(retirada,ct);}
  var minutos=agenda?.MinutosAgendados??input.MinutosAgendados;
  if(input.MinutosDisponiveis==0&&minutos>0)throw new ArgumentException("Uma agenda com consultas não pode ter capacidade disponível igual a zero. Confira a escala.");
  var row=new MedicaoPiloto{Data=input.Data.Date,Periodo=input.Periodo,Revisao=(anterior?.Revisao??0)+1,
   Realizadas=agenda?.Realizadas??input.Realizadas!.Value,Faltas=agenda?.Faltas??input.Faltas!.Value,
   Canceladas=agenda?.Canceladas??input.Canceladas!.Value,SemDesfecho=agenda?.SemDesfecho??input.SemDesfecho!.Value,
   MinutosAgendados=minutos,MinutosDisponiveis=input.MinutosDisponiveis,MinutosRecepcao=input.MinutosRecepcao,
   CustoHoraRecepcao=input.CustoHoraRecepcao,Fonte=input.Fonte.Trim(),Motivo=input.Motivo.Trim(),
   AgendaHash=agenda?.Hash,RegistradoEm=DateTime.UtcNow,RegistradoPor=usuario};
  return await PersistirAsync(row,ct);
 }
 private async Task<MedicaoPiloto> PersistirAsync(MedicaoPiloto row,CancellationToken ct) {
  db.MedicoesPiloto.Add(row);
  try{await db.SaveChangesAsync(ct);}catch(DbUpdateException e)when(e.InnerException is Npgsql.PostgresException {SqlState:"23505"}) {
   db.Entry(row).State=EntityState.Detached;throw new MedicaoConflitoException("Este dia foi registrado por outra pessoa. Recarregue para conferir os dados.");
  }
  return row;
 }
 public async Task<ComparacaoPiloto> CompararAsync(DateTime antesInicio,DateTime antesFim,DateTime pilotoInicio,DateTime pilotoFim,CancellationToken ct=default)=>(await RelatorioAsync(antesInicio,antesFim,pilotoInicio,pilotoFim,ct)).Comparacao;
 public async Task<RelatorioPiloto> RelatorioAsync(DateTime antesInicio,DateTime antesFim,DateTime pilotoInicio,DateTime pilotoFim,CancellationToken ct=default) {
  void Periodo(DateTime a,DateTime b){ValidarDia(a,"Antes");ValidarDia(b,"Piloto");if(b.Date<a.Date||(b.Date-a.Date).TotalDays>365)throw new ArgumentException("Cada período deve ter entre 1 e 366 dias encerrados.");}
  Periodo(antesInicio,antesFim);Periodo(pilotoInicio,pilotoFim);
  if(antesFim.Date>=pilotoInicio.Date)throw new ArgumentException("O período Antes deve terminar antes do início do Piloto, sem sobreposição.");
  var rows=await db.MedicoesPiloto.AsNoTracking().Where(x=>(x.Periodo=="Antes"&&x.Data>=antesInicio.Date&&x.Data<=antesFim.Date)||(x.Periodo=="Piloto"&&x.Data>=pilotoInicio.Date&&x.Data<=pilotoFim.Date)).ToListAsync(ct);
  var ultimas=rows.GroupBy(x=>new{x.Periodo,x.Data}).Select(x=>x.OrderByDescending(y=>y.Revisao).First()).OrderBy(x=>x.Data).ToArray();
  var a=Resumir(ultimas,"Antes",antesInicio.Date,antesFim.Date);var p=Resumir(ultimas,"Piloto",pilotoInicio.Date,pilotoFim.Date);
  var comparacao=new ComparacaoPiloto(a,p,p.TaxaFaltas==null||a.TaxaFaltas==null?null:Math.Round(p.TaxaFaltas.Value-a.TaxaFaltas.Value,1),
   p.Ocupacao==null||a.Ocupacao==null?null:Math.Round(p.Ocupacao.Value-a.Ocupacao.Value,1),
   p.MinutosPorConsulta-a.MinutosPorConsulta,p.CustoPorConsulta-a.CustoPorConsulta);
  return new(comparacao,ultimas.Where(x=>!x.Desconsiderada).ToArray());
 }
 public static PilotoPeriodo Resumir(IEnumerable<MedicaoPiloto> rows,string periodo,DateTime inicio,DateTime fim) {
  var dias=rows.Where(x=>x.Periodo==periodo&&x.Data>=inicio&&x.Data<=fim).GroupBy(x=>x.Data).Select(x=>x.OrderByDescending(y=>y.Revisao).First()).Where(x=>!x.Desconsiderada).ToArray();
  var ocupacao=dias.Where(x=>x.MinutosAgendados!=null&&x.MinutosDisponiveis!=null).ToArray();
  var recepcao=dias.Where(x=>x.MinutosRecepcao!=null).ToArray();var custos=recepcao.Where(x=>x.CustoHoraRecepcao!=null).ToArray();
  long ag=ocupacao.Sum(x=>(long)x.MinutosAgendados!.Value),cap=ocupacao.Sum(x=>(long)x.MinutosDisponiveis!.Value);
  long rec=recepcao.Sum(x=>(long)x.MinutosRecepcao!.Value),nc=recepcao.Sum(x=>(long)x.Consultas),ncc=custos.Sum(x=>(long)x.Consultas);
  decimal? custo=custos.Length==0?null:Math.Round(custos.Sum(x=>x.MinutosRecepcao!.Value*x.CustoHoraRecepcao!.Value/60m),2);
  var realizadas=dias.Sum(x=>x.Realizadas);var faltas=dias.Sum(x=>x.Faltas);
  return new(inicio,fim,(fim-inicio).Days+1,dias.Length,realizadas,faltas,dias.Sum(x=>x.Canceladas),dias.Sum(x=>x.SemDesfecho),OperacaoService.Taxa(faltas,realizadas+faltas),
   ocupacao.Length,ag,cap,cap==0?null:Math.Round(100d*ag/cap,1),recepcao.Length,rec,nc,nc==0?null:Math.Round((decimal)rec/nc,2),
   custos.Length,custo,ncc,ncc==0?null:Math.Round(custo!.Value/ncc,2));
 }
}
