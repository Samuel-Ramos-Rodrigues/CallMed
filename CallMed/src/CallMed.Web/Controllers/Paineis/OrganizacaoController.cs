using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MKSANCrud.Services.Usuarios;
namespace MKSANCrud.Controllers;
[Authorize,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class OrganizacaoController:Controller {
 public IActionResult Index()=>View(NavegacaoCallMed.Itens(User));
}
