using Microsoft.AspNetCore.Authentication;
namespace MKSANCrud.Services.Mobile;
public static class ClinicalAccess {
 public static async Task EncerrarAsync(HttpContext http){var schemes=http.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();if(await schemes.GetSchemeAsync("CallMedClinical")!=null)await http.SignOutAsync("CallMedClinical");}
}
