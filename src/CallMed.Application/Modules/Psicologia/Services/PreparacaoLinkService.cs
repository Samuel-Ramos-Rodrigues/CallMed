using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
namespace MKSANCrud.Services.Psicologia;
public sealed class PreparacaoLinkService(IDataProtectionProvider dp,IConfiguration config) {
 public string? Criar(Consulta c){var origin=PublicOrigin.TentarObter(config);if(origin==null)return null;var payload=JsonSerializer.Serialize(new Link(c.Id,c.PacienteId,JornadaService.Snapshot(c),DateTime.UtcNow.AddDays(4)));return new Uri(origin,"preparacao?token="+Uri.EscapeDataString(dp.CreateProtector("CallMed.Preparacao.v1").Protect(payload))).AbsoluteUri;}
 public Link? Ler(string? token){try{var x=JsonSerializer.Deserialize<Link>(dp.CreateProtector("CallMed.Preparacao.v1").Unprotect(token??""));return x?.ExpiraEm>DateTime.UtcNow?x:null;}catch(Exception e)when(e is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException){return null;}}
 public sealed record Link(int ConsultaId,int PacienteId,string Snapshot,DateTime ExpiraEm);
}
