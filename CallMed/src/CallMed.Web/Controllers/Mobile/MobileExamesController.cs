using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Mobile;

namespace MKSANCrud.Controllers.Mobile;

[ApiController, Route("api/mobile/v1/exames"), Authorize(Policy = MobileAuth.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MobileExamesController(MKSANContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pacienteId = (int)HttpContext.Items[MobileAuth.PacienteItem]!;
        return Ok(await db.ExamesHistorico.AsNoTracking()
            .Where(e => e.PacienteId == pacienteId && e.Ativo && e.StatusTriagem == ExameTriagemStatus.Aprovado)
            .OrderByDescending(e => e.DataRealizacao).ThenByDescending(e => e.Id).Take(200)
            .Select(e => new { e.Id, e.Nome, e.DataRealizacao, e.LocalRealizacao }).ToListAsync(ct));
    }
}
