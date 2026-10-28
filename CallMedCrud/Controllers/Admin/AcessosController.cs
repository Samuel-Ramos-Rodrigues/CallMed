using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AcessosController : Controller
{
    private readonly MKSANContext _context;

    public AcessosController(MKSANContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index() => View(new AcessosViewModel
    {
        Medicos = await _context.Medicos.AsNoTracking()
            .Include(m => m.EspecialidadeCadastro).OrderBy(m => m.Nome).ToListAsync(),
        Atendentes = await _context.Funcionarios.AsNoTracking()
            .Where(f => f.Cargo.ToLower() != "administrador").OrderBy(f => f.Nome).ToListAsync()
    });
}
