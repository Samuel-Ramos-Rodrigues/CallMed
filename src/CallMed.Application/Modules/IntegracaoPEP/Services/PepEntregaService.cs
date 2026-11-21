using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
namespace MKSANCrud.Services.Clinica;
public sealed class PepEntregaService(MKSANContext db,IHttpClientFactory clients,IConfiguration config){
 public async Task<int> ProcessarAsync(CancellationToken ct){if(!config.GetValue("PepIntegration:Enabled",false))return 0;var endpoint=config["PepIntegration:WebhookUrl"];var secret=config["PepIntegration:WebhookSecret"];if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo!=""||string.IsNullOrEmpty(secret)||secret.Length<32)return 0;var rows=await db.PepEventos.Include(x=>x.Consulta).Where(x=>x.EntregueEm==null&&x.Tentativas<10&&(x.TentarEm==null||x.TentarEm<=DateTime.UtcNow)).OrderBy(x=>x.CriadoEm).Take(20).ToListAsync(ct);var n=0;
  foreach(var e in rows){e.Tentativas++;e.TentarEm=DateTime.UtcNow.AddMinutes(Math.Min(360,Math.Pow(2,e.Tentativas)));await db.SaveChangesAsync(ct);var body=JsonSerializer.Serialize(new{specversion="1.0",id=e.Id,type="consulta.atualizada",time=e.CriadoEm,source="callmed",data=new{consultaId=e.ConsultaId,pacienteId=e.PacienteId,medicoId=e.MedicoId,status=e.Estado,data=e.Data.ToString("yyyy-MM-dd"),horario=e.Horario}});var ts=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();using var req=new HttpRequestMessage(HttpMethod.Post,uri){Content=new StringContent(body,Encoding.UTF8,"application/cloudevents+json")};req.Headers.Add("Idempotency-Key",e.Id.ToString());req.Headers.Add("X-CallMed-Timestamp",ts);req.Headers.Add("X-CallMed-Signature","sha256="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(ts+"."+body))).ToLowerInvariant());try{using var resp=await clients.CreateClient("CallMedPEP").SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);if(resp.IsSuccessStatusCode){e.EntregueEm=DateTime.UtcNow;n++;}}catch(HttpRequestException){}catch(TaskCanceledException)when(!ct.IsCancellationRequested){}await db.SaveChangesAsync(ct);
  }return n;
 }
}
public sealed class PepEntregaBackgroundService(IServiceScopeFactory scopes,ILogger<PepEntregaBackgroundService> logger):BackgroundService{
 protected override async Task ExecuteAsync(CancellationToken ct){using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1));do{try{using var s=scopes.CreateScope();await s.ServiceProvider.GetRequiredService<PepEntregaService>().ProcessarAsync(ct);await s.ServiceProvider.GetRequiredService<FhirPsicologiaService>().ProcessarAsync(ct);await OperacaoService.RegistrarAsync(s.ServiceProvider,"pep",true,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception e){logger.LogError(e,"Falha ao entregar evento ao PEP.");using var s=scopes.CreateScope();await OperacaoService.RegistrarAsync(s.ServiceProvider,"pep",false,ct);}}while(await timer.WaitForNextTickAsync(ct));}
}
