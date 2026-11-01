using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class SolicitacoesPainelViewModel
{
    public IReadOnlyList<SolicitacaoPainelItemViewModel> Registros { get; init; } = [];
    public string? Busca { get; init; }
    public string? Canal { get; init; }
    public string Aba { get; init; } = "solicitacoes";
    public string SituacaoConsulta { get; init; } = "todas";
    public int PaginaConsulta { get; init; } = 1;
    public const int ConsultasPorPagina = 20;
    public IReadOnlyList<SolicitacaoPainelItemViewModel> PedidosAbertos => Registros
        .Where(x => x.Consulta is null && x.Etapa is StatusSolicitacaoAtendimento.Nova or StatusSolicitacaoAtendimento.EmTriagem or StatusSolicitacaoAtendimento.BuscandoHorario or StatusSolicitacaoAtendimento.AguardandoPaciente)
        .OrderBy(x => x.CriadoEm).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> HistoricoPedidos => Registros
        .Where(x => x.Consulta is null && x.Etapa is not (StatusSolicitacaoAtendimento.Nova or StatusSolicitacaoAtendimento.EmTriagem or StatusSolicitacaoAtendimento.BuscandoHorario or StatusSolicitacaoAtendimento.AguardandoPaciente))
        .OrderByDescending(x => x.CriadoEm).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> Consultas => Registros.Where(x => x.Consulta is not null)
        .OrderBy(x => x.AguardandoConfirmacao ? 0 : x.Consulta!.Status == ConsultaStatus.Confirmada ? 1 : 2)
        .ThenBy(x => x.AguardandoConfirmacao || x.Consulta!.Status == ConsultaStatus.Confirmada ? x.Consulta!.Data.Ticks : -x.Consulta!.Data.Ticks)
        .ThenBy(x => x.Consulta!.Horario).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> ConsultasAgendadas => Consultas
        .Where(x => x.Consulta!.Status == ConsultaStatus.Confirmada).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> HistoricoConsultas => Consultas.Where(x => x.Historico).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> ConsultasFiltradas => SituacaoConsulta switch
    {
        "pendentes" => Pendentes,
        "agendadas" => ConsultasAgendadas,
        "realizadas" => Realizadas,
        "historico" => HistoricoConsultas,
        _ => Consultas
    };
    public int TotalPaginasConsulta => Math.Max(1, (ConsultasFiltradas.Count + ConsultasPorPagina - 1) / ConsultasPorPagina);
    public int PaginaAtualConsulta => Math.Clamp(PaginaConsulta, 1, TotalPaginasConsulta);
    public IReadOnlyList<SolicitacaoPainelItemViewModel> ConsultasDaPagina => ConsultasFiltradas
        .Skip((PaginaAtualConsulta - 1) * ConsultasPorPagina).Take(ConsultasPorPagina).ToList();
    public int TotalRecebidas => Registros.Count(x => x.Etapa is StatusSolicitacaoAtendimento.Nova or StatusSolicitacaoAtendimento.EmTriagem);
    public int TotalBuscando => Registros.Count(x => x.Etapa == StatusSolicitacaoAtendimento.BuscandoHorario);
    public int TotalAguardando => Registros.Count(x => x.Etapa == StatusSolicitacaoAtendimento.AguardandoPaciente);
    public int TotalConfirmadas => Registros.Count(x => x.Etapa == StatusSolicitacaoAtendimento.Confirmada);
    public IReadOnlyList<SolicitacaoPainelItemViewModel> Pendentes => Registros.Where(x => x.AguardandoConfirmacao)
        .OrderBy(x => x.Consulta!.Data).ThenBy(x => x.Consulta!.Horario).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> Realizadas => Registros.Where(x => x.Realizada)
        .OrderByDescending(x => x.Consulta!.RealizadaEm ?? x.Consulta!.Data).ThenByDescending(x => x.Consulta!.Horario).ToList();
    public IReadOnlyList<SolicitacaoPainelItemViewModel> Historico => Registros.Where(x => x.Historico)
        .OrderByDescending(x => x.Consulta?.Data ?? x.CriadoEm).ToList();

    public static IReadOnlyList<SolicitacaoPainelItemViewModel> Unificar(
        IEnumerable<SolicitacaoAtendimento> solicitacoes, IEnumerable<Consulta> consultas, DateTime hoje)
    {
        var porConsulta = consultas.ToDictionary(x => x.Id);
        var exibidas = new HashSet<int>();
        var registros = new List<SolicitacaoPainelItemViewModel>();
        foreach (var pedido in solicitacoes)
        {
            var consulta = pedido.Consulta;
            if (pedido.ConsultaId.HasValue && porConsulta.TryGetValue(pedido.ConsultaId.Value, out var atual))
                consulta = atual;
            if (consulta is not null && !exibidas.Add(consulta.Id)) continue;
            registros.Add(new() { Solicitacao = pedido, Consulta = consulta, Hoje = hoje });
        }
        foreach (var consulta in porConsulta.Values)
            if (exibidas.Add(consulta.Id)) registros.Add(new() { Consulta = consulta, Hoje = hoje });
        return registros;
    }
}
