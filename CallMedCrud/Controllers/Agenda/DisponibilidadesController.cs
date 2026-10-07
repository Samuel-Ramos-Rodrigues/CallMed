using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed class DisponibilidadesController : Controller
{
    private readonly MKSANContext _context;
    public DisponibilidadesController(MKSANContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var medicos = await _context.Medicos.AsNoTracking()
            .Include(m => m.EspecialidadeCadastro)
            .Include(m => m.HorariosSemanais)
            .Where(m => m.Ativo)
            .OrderBy(m => m.Nome)
            .ToListAsync(ct);
        return View(medicos);
    }
}
