using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Funcionario,Admin")]
public class FuncionarioPainelController : Controller
{
    private readonly MKSANContext _context;
    private readonly IClinicaClock _clock;
    private readonly UsuarioVinculoService _vinculos;

    public FuncionarioPainelController(
        MKSANContext context,
        IClinicaClock clock,
        UsuarioVinculoService vinculos)
    {
        _context = context;
        _clock = clock;
        _vinculos = vinculos;
    }

    public async Task<IActionResult> Index(int dias = 7)
    {
        var funcionario = await _vinculos.ObterFuncionarioAsync(User);
        if (!User.IsInRole("Admin") && (funcionario is null || !funcionario.Ativo))
            return Forbid();

        var hoje = _clock.Hoje;
        dias = dias is 7 or 14 or 30 ? dias : 7;

        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
        var fimMes = inicioMes.AddMonths(1);
        var consultasMes = await _context.Consultas
            .AsNoTracking()
            .Where(c => c.Data.Date >= inicioMes && c.Data.Date < fimMes)
            .ToListAsync();
        var vagasLivresMes = await _context.Disponibilidades.CountAsync(d =>
            d.Ativo && d.Data.HasValue && d.Data.Value.Date >= inicioMes && d.Data.Value.Date < fimMes);
        var ocupadasMes = consultasMes.Count(c => c.Status != ConsultaStatus.Cancelada);
        var capacidadeMes = vagasLivresMes + ocupadasMes;
        var conversasAbertas = await _context.ConversasAtendimento.CountAsync(c => c.Ativa);
        var conversasIa = await _context.ConversasAtendimento.CountAsync(c => c.Ativa && c.Modo == MKSANCrud.Models.Atendimento.ModoAtendimento.IA);
        var conversasHumano = await _context.ConversasAtendimento.CountAsync(c => c.Ativa && c.Modo == MKSANCrud.Models.Atendimento.ModoAtendimento.Humano);
        var conversasTotal = conversasAbertas;

        var inicioMesUtc = _clock.ConverterParaUtc(inicioMes);
        var fimMesUtc = _clock.ConverterParaUtc(fimMes);
        var hojeUtc = _clock.ConverterParaUtc(hoje);
        var amanhaUtc = _clock.ConverterParaUtc(hoje.AddDays(1));
        var solicitacoesMes = await _context.SolicitacoesAtendimento
            .AsNoTracking()
            .Where(x => x.CriadoEm >= inicioMesUtc && x.CriadoEm < fimMesUtc)
            .Select(x => new { x.Canal, x.Status, x.CriadoEm, x.ConfirmadaEm })
            .ToListAsync();
        var solicitacoesHoje = solicitacoesMes.Count(x => x.CriadoEm >= hojeUtc && x.CriadoEm < amanhaUtc);
        var solicitacoesPendentes = await _context.SolicitacoesAtendimento.AsNoTracking().CountAsync(x =>
            x.Status != StatusSolicitacaoAtendimento.Confirmada &&
            x.Status != StatusSolicitacaoAtendimento.Cancelada &&
            x.Status != StatusSolicitacaoAtendimento.Encerrada);
        var limiteAtraso = DateTime.UtcNow.AddMinutes(-30);
        var solicitacoesAtrasadas = await _context.SolicitacoesAtendimento.AsNoTracking().CountAsync(x =>
            x.CriadoEm < limiteAtraso &&
            x.Status != StatusSolicitacaoAtendimento.Confirmada &&
            x.Status != StatusSolicitacaoAtendimento.Cancelada &&
            x.Status != StatusSolicitacaoAtendimento.Encerrada);
        var taxaConfirmacaoSolicitacoes = solicitacoesMes.Count == 0
            ? 0
            : solicitacoesMes.Count(x => x.Status == StatusSolicitacaoAtendimento.Confirmada) * 100.0 / solicitacoesMes.Count;
        var temposConfirmacao = solicitacoesMes
            .Where(x => x.ConfirmadaEm.HasValue && x.ConfirmadaEm.Value >= x.CriadoEm)
            .Select(x => (x.ConfirmadaEm!.Value - x.CriadoEm).TotalMinutes)
            .ToList();
        var tempoMedioConfirmacao = temposConfirmacao.Count == 0 ? 0 : temposConfirmacao.Average();
        var porCanal = solicitacoesMes
            .GroupBy(x => x.Canal.ToString())
            .Select(g => new RankingItemViewModel { Nome = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .ToList();
        var ausenciasMes = consultasMes.Count(c => c.Status == ConsultaStatus.Ausente);
        var atendimentosFechadosMes = consultasMes.Count(c => c.Status == ConsultaStatus.Ausente || c.Status == ConsultaStatus.Realizada);
        var taxaAbsenteismo = atendimentosFechadosMes == 0 ? 0 : ausenciasMes * 100.0 / atendimentosFechadosMes;
        var vagasRecuperadas = await _context.AuditoriaEventos.AsNoTracking().CountAsync(x =>
            x.Acao == "Aceitar vaga" && x.CriadoEm >= inicioMesUtc && x.CriadoEm < fimMesUtc);

        var inicioGrafico = hoje.AddDays(-(dias - 1));
        var consultasGrafico = await _context.Consultas
            .AsNoTracking()
            .Where(c => c.Data.Date >= inicioGrafico && c.Data.Date <= hoje)
            .Select(c => new { c.Data, c.Status })
            .ToListAsync();
        var serieConsultas = Enumerable.Range(0, dias)
            .Select(i => inicioGrafico.AddDays(i))
            .Select(data => new DashboardSerieItemViewModel
            {
                Data = data,
                Total = consultasGrafico.Count(x => x.Data.Date == data.Date && x.Status != ConsultaStatus.Cancelada),
                Canceladas = consultasGrafico.Count(x => x.Data.Date == data.Date && x.Status == ConsultaStatus.Cancelada)
            })
            .ToList();

        var ranking = await _context.Consultas
            .AsNoTracking()
            .Include(c => c.Medico)
            .Where(c => c.Data.Date >= inicioMes && c.Data.Date < fimMes && c.Status != ConsultaStatus.Cancelada && c.Medico != null)
            .GroupBy(c => c.Medico!.Especialidade)
            .Select(g => new RankingItemViewModel { Nome = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToListAsync();

        var model = new FuncionarioPainelViewModel
        {
            Hoje = hoje,
            NomeUsuario = funcionario?.Nome ?? User.Identity?.Name?.Split('@')[0] ?? "Equipe",
            TotalPacientes = await _context.Pacientes.CountAsync(p => p.Ativo),
            MedicosAtivos = await _context.Medicos.CountAsync(m => m.Ativo),
            ConsultasHoje = await _context.Consultas.CountAsync(c => c.Data.Date == hoje && c.Status != ConsultaStatus.Cancelada),
            AguardandoHoje = await _context.Consultas.CountAsync(c => c.Data.Date == hoje && c.Status == ConsultaStatus.Pendente),
            ConfirmadasHoje = await _context.Consultas.CountAsync(c => c.Data.Date == hoje && c.Status == ConsultaStatus.Confirmada),
            CanceladasHoje = await _context.Consultas.CountAsync(c => c.Data.Date == hoje && c.Status == ConsultaStatus.Cancelada),
            Pendentes = consultasMes.Count(c => c.Status == ConsultaStatus.Pendente),
            Confirmadas = consultasMes.Count(c => c.Status == ConsultaStatus.Confirmada),
            ConsultasMes = consultasMes.Count,
            CancelamentosMes = consultasMes.Count(c => c.Status == ConsultaStatus.Cancelada),
            ListaEsperaAtiva = await _context.ListasEspera.CountAsync(x => x.Ativa),
            SolicitacoesHoje = solicitacoesHoje,
            SolicitacoesPendentes = solicitacoesPendentes,
            SolicitacoesAtrasadas = solicitacoesAtrasadas,
            TaxaConfirmacaoSolicitacoes = taxaConfirmacaoSolicitacoes,
            TempoMedioConfirmacaoMinutos = tempoMedioConfirmacao,
            AusenciasMes = ausenciasMes,
            TaxaAbsenteismo = taxaAbsenteismo,
            VagasRecuperadasListaEspera = vagasRecuperadas,
            SolicitacoesPorCanal = porCanal,
            ConversasIA = conversasIa,
            ConversasTotal = conversasTotal,
            ConversasAbertas = conversasAbertas,
            ConversasHumano = conversasHumano,
            SerieConsultas = serieConsultas,
            PeriodoDias = dias,
            OcupacaoAgenda = capacidadeMes <= 0 ? 0 : Math.Min(100, ocupadasMes * 100.0 / capacidadeMes),
            EspecialidadesMaisProcuradas = ranking,
            ProximasConsultas = await _context.Consultas
                .AsNoTracking().Include(c => c.Paciente).Include(c => c.Medico)
                .Where(c => c.Data.Date >= hoje && c.Status != ConsultaStatus.Cancelada)
                .OrderBy(c => c.Data).ThenBy(c => c.Horario).Take(6).ToListAsync()
        };

        ViewData["NotificationCount"] = model.Pendentes + model.ConversasAbertas + model.ListaEsperaAtiva + model.SolicitacoesPendentes;
        return View(model);
    }
}
