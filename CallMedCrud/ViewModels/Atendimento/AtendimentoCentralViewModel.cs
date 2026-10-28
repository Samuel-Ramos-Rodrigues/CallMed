using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.ViewModels;

public sealed class AtendimentoCentralViewModel
{
    public IReadOnlyList<ConversaResumoViewModel> Conversas { get; init; } =
        Array.Empty<ConversaResumoViewModel>();

    public ConversaAtendimento? ConversaSelecionada { get; init; }

    public IReadOnlyList<MensagemAtendimento> Mensagens { get; init; } =
        Array.Empty<MensagemAtendimento>();

    public IReadOnlyList<PacienteOpcaoAtendimentoViewModel> PacientesParaVinculo { get; init; } =
        Array.Empty<PacienteOpcaoAtendimentoViewModel>();

    public string? FiltroCanal { get; init; }
    public string? FiltroModo { get; init; }
}
