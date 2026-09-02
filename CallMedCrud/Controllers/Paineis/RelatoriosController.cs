using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public class RelatoriosController : Controller
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;

    public RelatoriosController(MKSANContext context, IClinicaClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IActionResult> Index()
    {
        var hoje = _clock.Hoje;
        var inicio = new DateTime(hoje.Year, hoje.Month, 1);
        var fim = inicio.AddMonths(1);

        var consultas = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .Where(c => c.Data.Date >= inicio && c.Data.Date < fim)
            .ToListAsync();

        var vagasLivres = await _context.Disponibilidades.CountAsync(d =>
            d.Ativo && d.Data.HasValue && d.Data.Value.Date >= inicio && d.Data.Value.Date < fim);
        var ocupadas = consultas.Count(c => c.Status != ConsultaStatus.Cancelada);
        var capacidade = vagasLivres + ocupadas;

        var inicioUtc = _clock.ConverterParaUtc(inicio);
        var fimUtc = _clock.ConverterParaUtc(fim);
        var solicitacoes = await _context.SolicitacoesAtendimento.AsNoTracking()
            .Where(x => x.CriadoEm >= inicioUtc && x.CriadoEm < fimUtc)
            .Select(x => new { x.Canal, x.Status, x.CriadoEm, x.ConfirmadaEm })
            .ToListAsync();
        var temposConfirmacao = solicitacoes.Where(x => x.ConfirmadaEm.HasValue && x.ConfirmadaEm.Value >= x.CriadoEm)
            .Select(x => (x.ConfirmadaEm!.Value - x.CriadoEm).TotalMinutes).ToList();
        var canais = solicitacoes.GroupBy(x => x.Canal.ToString())
            .Select(g => new RankingItemViewModel { Nome = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total).ToList();
        var ausentes = consultas.Count(x => x.Status == ConsultaStatus.Ausente);
        var fechadas = consultas.Count(x => x.Status == ConsultaStatus.Ausente || x.Status == ConsultaStatus.Realizada);
        var vagasRecuperadas = await _context.AuditoriaEventos.AsNoTracking().CountAsync(x =>
            x.Acao == "Aceitar vaga" && x.CriadoEm >= inicioUtc && x.CriadoEm < fimUtc);

        var ranking = consultas
            .Where(c => c.Status != ConsultaStatus.Cancelada && c.Medico is not null)
            .GroupBy(c => c.Medico!.Especialidade)
            .Select(g => new RankingItemViewModel { Nome = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        var model = new RelatorioGeralViewModel
        {
            InicioMes = inicio,
            Consultas = consultas.Count,
            Confirmadas = consultas.Count(c => c.Status == ConsultaStatus.Confirmada),
            Pendentes = consultas.Count(c => c.Status == ConsultaStatus.Pendente),
            Canceladas = consultas.Count(c => c.Status == ConsultaStatus.Cancelada),
            PacientesAtivos = await _context.Pacientes.CountAsync(p => p.Ativo),
            MedicosAtivos = await _context.Medicos.CountAsync(m => m.Ativo),
            ListaEsperaAtiva = await _context.ListasEspera.CountAsync(x => x.Ativa),
            ConversasAbertas = await _context.ConversasAtendimento.CountAsync(x => x.Ativa),
            OcupacaoAgenda = capacidade <= 0 ? 0 : Math.Min(100, ocupadas * 100.0 / capacidade),
            Ausentes = ausentes,
            TaxaAbsenteismo = fechadas == 0 ? 0 : ausentes * 100.0 / fechadas,
            Solicitacoes = solicitacoes.Count,
            SolicitacoesConfirmadas = solicitacoes.Count(x => x.Status == StatusSolicitacaoAtendimento.Confirmada),
            TaxaConfirmacaoSolicitacoes = solicitacoes.Count == 0 ? 0 : solicitacoes.Count(x => x.Status == StatusSolicitacaoAtendimento.Confirmada) * 100.0 / solicitacoes.Count,
            TempoMedioConfirmacaoMinutos = temposConfirmacao.Count == 0 ? 0 : temposConfirmacao.Average(),
            VagasRecuperadasListaEspera = vagasRecuperadas,
            SolicitacoesPorCanal = canais,
            Especialidades = ranking
        };

        return View(model);
    }
}
