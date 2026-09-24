using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin,Paciente,Medico")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HistoricoExamesController(
    MKSANContext context, UsuarioVinculoService vinculos, AuditoriaService auditoria, IClinicaClock clock) : Controller
{
    private bool Equipe => User.IsInRole("Funcionario") || User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> Index(int? pacienteId, CancellationToken ct)
    {
        // Os atalhos "Meus exames" não precisam expor um ID na URL.
        // IDs informados explicitamente continuam sujeitos à validação de acesso.
        if (pacienteId is null && User.IsInRole("Paciente") && !Equipe)
            pacienteId = (await vinculos.ObterPacienteAsync(User, ct))?.Id;

        if (pacienteId is not > 0 || !await PodeAcessarAsync(pacienteId.Value, ct)) return Forbid();
        return await PaginaAsync(pacienteId.Value,
            new ExameHistorico { PacienteId = pacienteId.Value, DataRealizacao = clock.Hoje }, ct);
    }

    [HttpPost]
    [Authorize(Roles = "Funcionario,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("PacienteId,Nome,DataRealizacao,LocalRealizacao", Prefix = "Novo")] ExameHistorico novo,
        CancellationToken ct)
    {
        if (!await PodeAcessarAsync(novo.PacienteId, ct)) return Forbid();
        if (novo.DataRealizacao == default || novo.DataRealizacao.Date > clock.Hoje)
            ModelState.AddModelError("Novo.DataRealizacao", "Informe uma data de realização válida, até hoje.");
        if (string.IsNullOrWhiteSpace(novo.Nome))
            ModelState.AddModelError("Novo.Nome", "Informe o nome do exame realizado.");
        if (!ModelState.IsValid) return await PaginaAsync(novo.PacienteId, novo, ct);

        novo.Nome = novo.Nome.Trim(); novo.LocalRealizacao = novo.LocalRealizacao?.Trim();
        novo.DataRealizacao = novo.DataRealizacao.Date;
        context.ExamesHistorico.Add(novo);
        await context.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Registrar exame", "Paciente", novo.PacienteId,
            $"Registro administrativo de exame #{novo.Id} adicionado ao histórico.", ct: ct);
        TempData["Sucesso"] = "Exame registrado no histórico.";
        return RedirectToAction(nameof(Index), new { pacienteId = novo.PacienteId });
    }

    [HttpPost]
    [Authorize(Roles = "Funcionario,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desativar(int id, CancellationToken ct)
    {
        var item = await context.ExamesHistorico.FirstOrDefaultAsync(x => x.Id == id && x.Ativo, ct);
        if (item is null) return NotFound();
        item.Ativo = false;
        await context.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Desativar exame", "Paciente", item.PacienteId,
            $"Registro administrativo de exame #{id} desativado; dados preservados.", ct: ct);
        return RedirectToAction(nameof(Index), new { pacienteId = item.PacienteId });
    }

    private async Task<bool> PodeAcessarAsync(int pacienteId, CancellationToken ct)
    {
        if (pacienteId <= 0 || !await context.Pacientes.AnyAsync(x => x.Id == pacienteId, ct)) return false;
        if (Equipe) return true;
        if (User.IsInRole("Paciente"))
            return (await vinculos.ObterPacienteAsync(User, ct))?.Id == pacienteId;
        var medico = await vinculos.ObterMedicoAsync(User, ct);
        return medico is not null && await context.Consultas.AnyAsync(x =>
            x.PacienteId == pacienteId && x.MedicoId == medico.Id && x.Status != ConsultaStatus.Cancelada, ct);
    }

    private async Task<IActionResult> PaginaAsync(int pacienteId, ExameHistorico novo, CancellationToken ct)
    {
        var paciente = await context.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pacienteId, ct);
        if (paciente is null) return NotFound();
        var exames = await context.ExamesHistorico.AsNoTracking().Where(x => x.PacienteId == pacienteId && x.Ativo)
            .OrderByDescending(x => x.DataRealizacao).ThenByDescending(x => x.Id).Take(200).ToListAsync(ct);
        return View("Index", new HistoricoExamesViewModel { Paciente = paciente, Exames = exames, Novo = novo });
    }
}
