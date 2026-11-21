using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
namespace MKSANCrud.Services.Psicologia;

public sealed class PsicologiaService(MKSANContext db,IClinicaClock clock)
{
 public static bool Menor(Paciente p,DateTime hoje)=>p.DataNascimento.HasValue && p.DataNascimento.Value.Date>hoje.Date.AddYears(-18);
 public async Task<string?> ImpedimentoAsync(Consulta c,CancellationToken ct)
 {
  var psi=c.Medico??await db.Psicologos.FindAsync([c.MedicoId],ct);
  if(psi?.HabilitadoClinicamente!=true)return "A clínica precisa cadastrar e verificar o registro do profissional no conselho correspondente antes de liberar o atendimento.";
  var p=c.Paciente??await db.Pacientes.FindAsync([c.PacienteId],ct);
  if(p?.DataNascimento==null)return "Complete a data de nascimento do paciente antes do atendimento.";
  if(Menor(p,clock.Hoje)&&!await db.AutorizacoesResponsaveis.AnyAsync(x=>x.PacienteId==c.PacienteId&&x.PsicologoId==c.MedicoId&&x.RevogadaEm==null&&x.ValidaAte>DateTime.UtcNow,ct))return "O profissional deve conferir e registrar a autorização do responsável legal para o atendimento de menor de idade.";
  return null;
 }
 public async Task<bool> EnvelopeValidoAsync(string envelope,string profissional,string? paciente,bool reservado,CancellationToken ct)
 {
  if(string.IsNullOrEmpty(envelope)||envelope.Length>150000)return false;
  try {
   using var doc=JsonDocument.Parse(envelope);var e=doc.RootElement;
   if(e.EnumerateObject().Count()!=4||e.EnumerateObject().Any(x=>x.Name is not ("versao" or "iv" or "ciphertext" or "destinatarios")))return false;
   if(e.GetProperty("versao").GetInt32()!=1||Convert.FromBase64String(e.GetProperty("iv").GetString()!).Length!=12||Convert.FromBase64String(e.GetProperty("ciphertext").GetString()!).Length is <16 or >65536)return false;
   var rs=e.GetProperty("destinatarios").EnumerateArray().ToArray();
   if(rs.Length is <1 or >16||rs.Any(x=>x.EnumerateObject().Count()!=2||x.EnumerateObject().Any(p=>p.Name is not ("id" or "key"))))return false;
   var ids=rs.Select(x=>x.GetProperty("id").GetGuid()).ToArray();
   if(ids.Distinct().Count()!=ids.Length||rs.Any(x=>Convert.FromBase64String(x.GetProperty("key").GetString()!).Length!=384))return false;
   var keys=await db.ChavesClinicas.AsNoTracking().Where(x=>ids.Contains(x.Id)&&x.RevogadaEm==null).ToListAsync(ct);
   return keys.Count==ids.Length&&keys.Any(x=>x.UsuarioId==profissional)&&keys.All(x=>x.UsuarioId==profissional||(!reservado&&x.UsuarioId==paciente))&&(reservado||keys.Any(x=>x.UsuarioId==paciente));
  }catch(Exception e)when(e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException or ArgumentException){return false;}
 }
 public static string Hash(string envelope)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(envelope))).ToLowerInvariant();
}
