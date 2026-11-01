using System.Linq.Expressions;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

public static class ConsultaPendencias
{
    // O painel e o sino usam o mesmo período e os mesmos estados.
    public static Expression<Func<Consulta, bool>> AguardandoConfirmacao(DateTime hoje) =>
        c => c.Data.Date >= hoje.Date &&
            (c.Status == ConsultaStatus.Pendente || c.Status == ConsultaStatus.Remarcada);

    public static Expression<Func<SolicitacaoAtendimento, bool>> SolicitacoesEmAberto =>
        x => x.ConsultaId == null &&
            (x.Status == StatusSolicitacaoAtendimento.Nova ||
             x.Status == StatusSolicitacaoAtendimento.EmTriagem ||
             x.Status == StatusSolicitacaoAtendimento.BuscandoHorario);
}
