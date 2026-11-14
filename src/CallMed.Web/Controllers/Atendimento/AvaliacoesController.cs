using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public sealed class AvaliacoesController(MKSANContext context, UsuarioVinculoService vinculos, IClinicaClock clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? mes, string? canal, int pagina = 1, CancellationToken ct = default)
    {
        if ((await vinculos.ObterFuncionarioAsync(User, ct))?.Ativo != true) return Forbid();
        var inicio = new DateTime(clock.Hoje.Year, clock.Hoje.Month, 1);
        if (!string.IsNullOrWhiteSpace(mes))
        {
            if (!DateTime.TryParseExact(mes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out inicio) || inicio.Year < 2000 || inicio.Year > 2100)
                return BadRequest("Selecione um mês válido.");
        }
        var desde = clock.ConverterParaUtc(inicio); var ate = clock.ConverterParaUtc(inicio.AddMonths(1));
        var query = context.AvaliacoesAtendimento.AsNoTracking().Where(a => a.CriadoEm >= desde && a.CriadoEm < ate);
        CanalAtendimento? filtro = Enum.TryParse<CanalAtendimento>(canal, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        if (filtro.HasValue) query = query.Where(a => a.Canal == filtro.Value);
        var resumo = await query.Select(a => new { a.Canal, a.Nota }).ToListAsync(ct);
        var respondidas = resumo.Where(a => a.Nota.HasValue).ToList();
        var total = respondidas.Count;
        var paginas = Math.Max(1, (int)Math.Ceiling(total / 30.0)); pagina = Math.Clamp(pagina, 1, paginas);
        var lista = await query.Where(a => a.Nota != null).OrderByDescending(a => a.RespondidoEm).ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * 30).Take(30).Select(a => new AvaliacaoItemViewModel
            { ConversaId = a.ConversaAtendimentoId, Canal = a.Canal, Nota = a.Nota!.Value, Comentario = a.Comentario, RespondidoEm = a.RespondidoEm!.Value }).ToListAsync(ct);
        foreach (var item in lista) item.RespondidoEm = clock.ConverterUtc(item.RespondidoEm);
        return View(new AvaliacoesViewModel
        {
            Mes = inicio.ToString("yyyy-MM"), Canal = filtro?.ToString(), Total = resumo.Count, Respondidas = total,
            Media = total == 0 ? null : respondidas.Average(a => (double)a.Nota!.Value),
            Satisfacao = total == 0 ? null : 100.0 * respondidas.Count(a => a.Nota >= 4) / total,
            PorCanal = resumo.GroupBy(a => a.Canal).Select(g => new AvaliacaoCanalViewModel { Canal = g.Key, Total = g.Count(),
                Respondidas = g.Count(a => a.Nota.HasValue), Media = g.Where(a => a.Nota.HasValue).Select(a => (double?)a.Nota).Average() }).ToList(),
            Itens = lista, Pagina = pagina, Paginas = paginas
        });
    }
}
