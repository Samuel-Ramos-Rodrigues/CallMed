using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class SolicitacaoDetalhesViewModel
{
    public SolicitacaoAtendimento Solicitacao { get; init; } = new();
    public IReadOnlyList<AuditoriaEvento> Historico { get; init; } = Array.Empty<AuditoriaEvento>();
}
