using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.ViewModels;

public sealed class AtendimentoCentralViewModel
{
    public IReadOnlyList<ConversaResumoViewModel> Conversas { get; init; } =
        Array.Empty<ConversaResumoViewModel>();

    public ConversaAtendimento? ConversaSelecionada { get; init; }
    public MKSANCrud.Services.Atendimento.AtendimentoIaStatus? EstadoIa { get; init; }

    public IReadOnlyList<MensagemAtendimento> Mensagens { get; init; } =
        Array.Empty<MensagemAtendimento>();

    public IReadOnlyList<PacienteOpcaoAtendimentoViewModel> PacientesParaVinculo { get; init; } =
        Array.Empty<PacienteOpcaoAtendimentoViewModel>();

    public string? FiltroCanal { get; init; }
    public string? FiltroModo { get; init; }

    // Somente dados administrativos já acessíveis à recepção. Sem prontuário clínico.
    public IReadOnlyList<ConsultaContextoAtendimento> ConsultasPaciente { get; init; } = [];
    public ConsultaContextoAtendimento? ProximaConsulta { get; init; }
}

public sealed record ConsultaContextoAtendimento(int Id, DateTime Data, string Horario,
    string Profissional, string Especialidade, string Status);
