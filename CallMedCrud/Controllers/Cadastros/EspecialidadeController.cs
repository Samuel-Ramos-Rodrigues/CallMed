using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public class EspecialidadeController : Controller
{
    private readonly MKSANContext _context;
    private readonly EspecialidadeService _especialidades;

    public EspecialidadeController(
        MKSANContext context,
        EspecialidadeService especialidades)
    {
        _context = context;
        _especialidades = especialidades;
    }

    public async Task<IActionResult> Index() =>
        View(await _context.Especialidades
            .AsNoTracking()
            .Include(e => e.Medicos)
            .Where(e => e.Medicos.Any())
            .OrderBy(e => e.Nome)
            .ToListAsync());

    // Na V14.1 a especialidade nasce do cadastro do médico.
    // Mantemos a rota antiga apenas para não quebrar favoritos/links existentes.
    public IActionResult Create()
    {
        TempData["Info"] = "Cadastre o médico e informe a especialidade. Se ela ainda não existir, o CallMed cria automaticamente.";
        return RedirectToAction("Create", "Medico");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind("Nome,Ativo")] Especialidade model)
    {
        TempData["Info"] = "As especialidades agora são criadas automaticamente pelo cadastro de médicos.";
        return RedirectToAction("Create", "Medico");
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.Especialidades.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,Ativo")] Especialidade model)
    {
        if (id != model.Id)
            return NotFound();

        var atual = await _context.Especialidades
            .Include(e => e.Medicos)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (atual is null)
            return NotFound();

        model.Nome = _especialidades.CanonicalizarNome(model.Nome);
        ModelState.Remove(nameof(Especialidade.Nome));

        if (string.IsNullOrWhiteSpace(model.Nome))
            ModelState.AddModelError(nameof(Especialidade.Nome), "Informe o nome da especialidade.");

        if (await _context.Especialidades.AnyAsync(e =>
                e.Id != id && e.Nome.ToLower() == model.Nome.ToLower()))
        {
            ModelState.AddModelError(nameof(Especialidade.Nome), "Essa especialidade já está cadastrada.");
        }

        if (!model.Ativo && atual.Medicos.Any(m => m.Ativo))
        {
            ModelState.AddModelError(
                nameof(Especialidade.Ativo),
                "Não é possível desativar uma especialidade que possui médicos ativos.");
        }

        if (!ModelState.IsValid)
            return View(model);

        atual.Nome = model.Nome;
        atual.Ativo = model.Ativo;

        // Mantém o texto legado sincronizado com o catálogo.
        foreach (var medico in atual.Medicos)
            medico.Especialidade = atual.Nome;

        await _context.SaveChangesAsync();
        TempData["Sucesso"] = "Especialidade atualizada.";
        return RedirectToAction(nameof(Index));
    }
}
