using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;
using MKSANCrud.ViewModels;

internal static class SolicitacoesPainelChecks
{
    public static void Run(Action<bool, string> check)
    {
        var hoje = new DateTime(2026, 10, 31);
        var paciente = new Paciente { Nome = "Paciente de teste", Cpf = "52998224725" };
        Consulta C(int id, string status, int dias = 1) => new()
        {
            Id = id, Status = status, Data = hoje.AddDays(dias), Horario = "09:30",
            Paciente = paciente, Medico = new Medico { Nome = "Médico de teste", Especialidade = "Cardiologia" }
        };
        var consultas = new[]
        {
            C(1, ConsultaStatus.Pendente, 0), C(2, ConsultaStatus.Pendente), C(3, ConsultaStatus.Remarcada),
            C(4, ConsultaStatus.Realizada, -2), C(5, ConsultaStatus.Realizada, -1),
            C(6, ConsultaStatus.Confirmada), C(7, ConsultaStatus.Cancelada), C(8, ConsultaStatus.Ausente, -1),
            C(9, ConsultaStatus.Pendente, -1)
        };
        var pedidos = new[]
        {
            new SolicitacaoAtendimento { Id = 10, Status = StatusSolicitacaoAtendimento.Encerrada, ConsultaId = 4, Consulta = consultas[3] },
            new SolicitacaoAtendimento { Id = 11, Status = StatusSolicitacaoAtendimento.Confirmada, ConsultaId = 3, Consulta = consultas[2] },
            new SolicitacaoAtendimento { Id = 12, Status = StatusSolicitacaoAtendimento.Nova, Paciente = paciente },
            new SolicitacaoAtendimento { Id = 13, Status = StatusSolicitacaoAtendimento.EmTriagem, Paciente = paciente },
            new SolicitacaoAtendimento { Id = 14, Status = StatusSolicitacaoAtendimento.BuscandoHorario, Paciente = paciente },
            new SolicitacaoAtendimento { Id = 15, Status = StatusSolicitacaoAtendimento.AguardandoPaciente, Paciente = paciente }
        };
        var model = new SolicitacoesPainelViewModel { Registros = SolicitacoesPainelViewModel.Unificar(pedidos, consultas, hoje) };
        check(model.Pendentes.Count == 3, "As três confirmações aparecem mesmo com consultas sem solicitação");
        check(model.Realizadas.Count == 2, "Realizadas vinculadas a pedidos encerrados e realizadas diretas permanecem visíveis");
        check(model.TotalConfirmadas == 1, "Consulta remarcada não aparece como confirmada pelo estado antigo do pedido");
        check(model.Registros.Count == 13 && model.Registros.Count(x => x.Consulta?.Id == 4) == 1,
            "Consulta vinculada aparece uma única vez no painel");
        check(model.TotalRecebidas == 2 && model.TotalBuscando == 1 && model.TotalAguardando == 1,
            "As etapas anteriores ao agendamento são preservadas");
        check(model.Historico.Count == 3 && model.Historico.Any(x => x.PendenteAnterior),
            "Canceladas, ausências e pendências de datas anteriores ficam no histórico");
        check(model.Registros.Count == model.Pendentes.Count + model.Realizadas.Count + model.Historico.Count +
            model.TotalRecebidas + model.TotalBuscando + model.TotalAguardando + model.TotalConfirmadas,
            "Todo registro pertence a uma única área visível");
        var sino = consultas.AsQueryable().Where(ConsultaPendencias.AguardandoConfirmacao(hoje)).Select(x => x.Id).Order().ToArray();
        check(sino.SequenceEqual(model.Pendentes.Select(x => x.Consulta!.Id).Order()),
            "O sino e o painel usam as mesmas consultas pendentes e o mesmo limite de data");
        check(model.Registros.Single(x => x.Consulta?.Id == 2).CanalNome == "Canal não informado",
            "Consulta antiga sem origem não recebe um canal inventado");
        var duplicada = SolicitacoesPainelViewModel.Unificar(pedidos.Append(pedidos[0]), consultas, hoje);
        check(duplicada.Count(x => x.Consulta?.Id == 4) == 1, "Dois pedidos com a mesma consulta não duplicam a consulta");
        var muitas = Enumerable.Range(100, 450).Select(id => C(id, ConsultaStatus.Realizada, -1)).Append(C(900, ConsultaStatus.Pendente));
        var volume = new SolicitacoesPainelViewModel { Registros = SolicitacoesPainelViewModel.Unificar([], muitas, hoje) };
        check(volume.Realizadas.Count == 450 && volume.Pendentes.Count == 1,
            "Histórico com mais de 400 consultas não oculta a confirmação pendente");
        check(model.PedidosAbertos.Count == 4 && model.Consultas.Count == 9 &&
            model.PedidosAbertos.Count + model.HistoricoPedidos.Count + model.Consultas.Count == model.Registros.Count,
            "As abas separam pedidos sem agendamento das consultas, sem perder registros");
        var realizadasFiltradas = new SolicitacoesPainelViewModel { Registros = model.Registros, Aba = "consultas", SituacaoConsulta = "realizadas" };
        check(realizadasFiltradas.ConsultasFiltradas.Count == 2 && realizadasFiltradas.ConsultasDaPagina.All(x => x.Realizada),
            "O filtro Realizadas não mistura consultas confirmadas ou pendentes");
        var ultimaPagina = new SolicitacoesPainelViewModel { Registros = volume.Registros, Aba = "consultas", SituacaoConsulta = "realizadas", PaginaConsulta = 999 };
        check(ultimaPagina.PaginaAtualConsulta == 23 && ultimaPagina.ConsultasDaPagina.Count == 10 && ultimaPagina.ConsultasFiltradas.Count == 450,
            "A paginação mantém o total completo e limita páginas fora do intervalo");
        var resumo = new ConfirmacoesViewModel { TotalPendencias = 12, Itens = [new(), new(), new()] };
        check(resumo.Total == 12, "O contador real do sino independe do número de itens da prévia");
    }
}
