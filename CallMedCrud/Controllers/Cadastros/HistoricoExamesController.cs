using System.Security.Claims;
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
        // O atalho "Meus exames" usa o paciente ligado à conta; IDs explícitos
        // continuam sujeitos à validação de acesso.
        if (pacienteId is null && User.IsInRole("Paciente") && !Equipe)
            pacienteId = (await vinculos.ObterPacienteAsync(User, ct))?.Id;

        if (pacienteId is not > 0 || !await PodeAcessarAsync(pacienteId.Value, ct)) return Forbid();
        if (Equipe)
        {
            ViewBag.ExamesPendentes = await context.ExamesHistorico.AsNoTracking()
                .CountAsync(x => x.Ativo && x.StatusTriagem == ExameTriagemStatus.Pendente, ct);
        }
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

        novo.Nome = novo.Nome.Trim();
        novo.LocalRealizacao = string.IsNullOrWhiteSpace(novo.LocalRealizacao) ? null : novo.LocalRealizacao.Trim();
        novo.DataRealizacao = novo.DataRealizacao.Date;
        novo.Ativo = true;
        // Todos os novos registros aguardam revisão administrativa; campos de
        // triagem não são aceitos do navegador.
        novo.StatusTriagem = ExameTriagemStatus.Pendente;
        novo.TriadoEm = null;
        novo.TriadoPorUsuarioId = null;
        novo.ObservacaoTriagem = null;

        context.ExamesHistorico.Add(novo);
        await context.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Enviar exame para triagem", "Paciente", novo.PacienteId,
            $"Registro de exame #{novo.Id} encaminhado para triagem administrativa.", ct: ct);
        TempData["Sucesso"] = "Exame enviado para triagem. Ele aparecerá no histórico após aprovação da equipe.";
        return RedirectToAction(nameof(Index), new { pacienteId = novo.PacienteId });
    }

    [HttpGet]
    [Authorize(Roles = "Funcionario,Admin")]
    public async Task<IActionResult> Pendentes(CancellationToken ct)
    {
        var itens = await context.ExamesHistorico.AsNoTracking()
            .Include(x => x.Paciente)
            .Where(x => x.Ativo && x.StatusTriagem == ExameTriagemStatus.Pendente)
            .OrderBy(x => x.CriadoEm)
            .ThenBy(x => x.Id)
            .Take(300)
            .ToListAsync(ct);
        return View(itens);
    }

    [HttpPost]
    [Authorize(Roles = "Funcionario,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprovar(int id, string? observacaoTriagem, CancellationToken ct)
    {
        var exame = await context.ExamesHistorico
            .FirstOrDefaultAsync(x => x.Id == id && x.Ativo, ct);
        if (exame is null) return NotFound();
        if (exame.StatusTriagem != ExameTriagemStatus.Pendente)
        {
            TempData["Erro"] = "Este registro já foi triado ou não está mais pendente.";
            return RedirectToAction(nameof(Pendentes));
        }

        exame.StatusTriagem = ExameTriagemStatus.Aprovado;
        exame.TriadoEm = DateTime.UtcNow;
        exame.TriadoPorUsuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        exame.ObservacaoTriagem = LimitarObservacao(observacaoTriagem);
        await context.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Aprovar exame", "Paciente", exame.PacienteId,
            $"Registro de exame #{exame.Id} aprovado para exibição no histórico.", ct: ct);
        TempData["Sucesso"] = "Exame aprovado e disponibilizado no histórico do paciente.";
        return RedirectToAction(nameof(Pendentes));
    }

    [HttpPost]
    [Authorize(Roles = "Funcionario,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rejeitar(int id, string? motivo, CancellationToken ct)
    {
        var exame = await context.ExamesHistorico
            .FirstOrDefaultAsync(x => x.Id == id && x.Ativo, ct);
        if (exame is null) return NotFound();
        if (exame.StatusTriagem != ExameTriagemStatus.Pendente)
        {
            TempData["Erro"] = "Este registro já foi triado ou não está mais pendente.";
            return RedirectToAction(nameof(Pendentes));
        }
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Erro"] = "Informe o motivo para rejeitar o registro.";
            return RedirectToAction(nameof(Pendentes));
        }

        exame.StatusTriagem = ExameTriagemStatus.Rejeitado;
        exame.TriadoEm = DateTime.UtcNow;
        exame.TriadoPorUsuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        exame.ObservacaoTriagem = LimitarObservacao(motivo);
        await context.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Rejeitar exame", "Paciente", exame.PacienteId,
            $"Registro de exame #{exame.Id} rejeitado na triagem administrativa.", ct: ct);
        TempData["Sucesso"] = "Registro rejeitado e mantido fora do histórico visível do paciente.";
        return RedirectToAction(nameof(Pendentes));
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
        var exames = await context.ExamesHistorico.AsNoTracking()
            .Where(x => x.PacienteId == pacienteId && x.Ativo && x.StatusTriagem == ExameTriagemStatus.Aprovado)
            .OrderByDescending(x => x.DataRealizacao).ThenByDescending(x => x.Id).Take(200).ToListAsync(ct);
        return View("Index", new HistoricoExamesViewModel { Paciente = paciente, Exames = exames, Novo = novo });
    }

    private static string? LimitarObservacao(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return texto.Length <= 600 ? texto : texto[..600];
    }
}
