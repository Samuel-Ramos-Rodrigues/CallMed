using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class SolicitacoesPainelViewModel
{
    public IReadOnlyList<SolicitacaoPainelItemViewModel> Registros { get; init; } = [];
    public string? Busca { get; init; }
    public string? Canal { get; init; }
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
