using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AuditoriaController : Controller
{
    private readonly MKSANContext _context;
    public AuditoriaController(MKSANContext context) => _context = context;

    public async Task<IActionResult> Index(string? busca, string? entidade, CancellationToken ct)
    {
        var q = _context.AuditoriaEventos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entidade)) q = q.Where(x => x.Entidade == entidade);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            q = q.Where(x =>
                (x.UsuarioNome != null && x.UsuarioNome.ToLower().Contains(termo)) ||
                x.Acao.ToLower().Contains(termo) ||
                x.Entidade.ToLower().Contains(termo) ||
                (x.Descricao != null && x.Descricao.ToLower().Contains(termo)));
        }
        var itens = await q.OrderByDescending(x => x.CriadoEm).Take(500).ToListAsync(ct);
        ViewData["Title"] = "Auditoria";
        ViewData["Subtitle"] = "Rastreabilidade de ações administrativas e alterações clínicas.";
        return View(new AuditoriaPageViewModel { Itens = itens, Busca = busca, Entidade = entidade });
    }
}
