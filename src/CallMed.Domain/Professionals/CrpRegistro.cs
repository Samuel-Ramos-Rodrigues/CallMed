using System.Security.Claims;
using System.Text.RegularExpressions;
namespace MKSANCrud.Services.Psicologia;

public static class CrpRegistro
{
    public static bool TryNormalizar(string? entrada,out string? valor)
    {
        valor=null;if(string.IsNullOrWhiteSpace(entrada))return false;
        var m=Regex.Match(entrada.Trim(),@"^(?:CRP[-\s]*)?(?<regional>\d{1,2})\s*/\s*(?<numero>\d{3,8})$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        if(!m.Success||!int.TryParse(m.Groups["regional"].Value,out var regional)||regional<1||m.Groups["numero"].Value.All(x=>x=='0'))return false;
        valor=regional.ToString("00")+"/"+m.Groups["numero"].Value;return true;
    }
}
public static class PsicologiaRoles
{
    public static bool IsProfissionalSaude(this ClaimsPrincipal user)=>user.IsInRole("Psicologo")||user.IsInRole("Medico");
    public static bool IsPsicologo(this ClaimsPrincipal user)=>user.IsInRole("Psicologo")||user.IsInRole("Medico");
}
