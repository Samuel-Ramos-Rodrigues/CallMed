using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ConveniosController : Controller
{
    private readonly MKSANContext _context;
    private readonly ConvenioElegibilidadeService _service;
    private readonly AuditoriaService _auditoria;

    public ConveniosController(MKSANContext context, ConvenioElegibilidadeService service, AuditoriaService auditoria)
    {
        _context = context;
        _service = service;
        _auditoria = auditoria;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Convênios e cobertura";
        ViewData["Subtitle"] = "Defina quais especialidades cada convênio cobre durante a triagem.";
        await PrepararAsync(ct);
        var regras = await _context.ConveniosEspecialidades.AsNoTracking()
            .Include(x => x.Especialidade)
            .Where(x => x.Ativo)
            .OrderBy(x => x.ConvenioNome).ThenBy(x => x.Especialidade!.Nome)
            .ToListAsync(ct);
        var convenios = await _context.Pacientes.AsNoTracking()
            .Where(x => x.TemConvenio && x.NomeConvenio != null && x.NomeConvenio != "")
            .Select(x => x.NomeConvenio!)
            .Distinct().OrderBy(x => x).ToListAsync(ct);
        return View(new ConveniosRegrasViewModel { Regras = regras, ConveniosCadastrados = convenios });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(string convenioNome, int especialidadeId, bool coberta, string? observacao, CancellationToken ct)
    {
        try
        {
            var regra = await _service.SalvarRegraAsync(convenioNome, especialidadeId, coberta, observacao, ct);
            await _auditoria.RegistrarAsync("Configurar cobertura", "Convênio", regra.Id, $"{regra.ConvenioNome} / especialidade {regra.EspecialidadeId}: {(regra.Coberta ? "coberta" : "não coberta")}", ct: ct);
            TempData["Sucesso"] = "Regra de cobertura salva.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Erro"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desativar(int id, CancellationToken ct)
    {
        var item = await _context.ConveniosEspecialidades.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        item.Ativo = false;
        item.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync("Desativar cobertura", "Convênio", id, $"Regra de {item.ConvenioNome} desativada.", ct: ct);
        TempData["Sucesso"] = "Regra desativada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PrepararAsync(CancellationToken ct)
    {
        ViewBag.Especialidades = await _context.Especialidades.AsNoTracking().Where(x => x.Ativo).OrderBy(x => x.Nome).ToListAsync(ct);
    }
}
