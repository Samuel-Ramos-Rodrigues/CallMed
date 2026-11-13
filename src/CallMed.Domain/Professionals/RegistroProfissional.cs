using System.Text.RegularExpressions;
using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public static class RegistroProfissional {
 public static readonly string[] Areas=["Medicina","Psicologia","Outra profissão de saúde"];
 public static bool Normalizar(string? area,string? conselho,string? numero,string? uf,out string c,out string n,out string u) {
  c=(conselho??"").Trim().ToUpperInvariant();n=(numero??"").Trim().ToUpperInvariant();u=(uf??"").Trim().ToUpperInvariant();
  if(!Areas.Contains(area))return false;
  if(area=="Psicologia"){c="CRP";u="";if(!CrpRegistro.TryNormalizar(n,out var crp))return false;n=crp!;return true;}
  if(area=="Medicina"){c="CRM";if(!Regex.IsMatch(n,@"^[0-9]{1,10}$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))||n.All(x=>x=='0'))return false;}
  else if(c is "CRM" or "CRP")return false;
  return Regex.IsMatch(c,@"^[A-Z][A-Z0-9-]{1,19}$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))&&
   Regex.IsMatch(n,@"^[A-Z0-9][A-Z0-9./-]{0,29}$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))&&
   Estados.Contains(u);
 }
 public static readonly string[] Estados=["AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA","PB","PR","PE","PI","RJ","RN","RS","RO","RR","SC","SP","SE","TO"];
 public static bool Valido(ProfissionalSaude p)=>p.EhPsicologia?CrpRegistro.TryNormalizar(p.Crp,out _):Normalizar(p.AreaProfissional,p.RegistroConselho,p.RegistroNumero,p.RegistroUf,out _,out _,out _);
}
