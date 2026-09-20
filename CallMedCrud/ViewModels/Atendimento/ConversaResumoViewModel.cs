using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.ViewModels;

public sealed class ConversaResumoViewModel
{
    public long Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Identificador { get; init; } = string.Empty;
    public CanalAtendimento Canal { get; init; }
    public ModoAtendimento Modo { get; init; }
    public bool Ativa { get; init; }
    public DateTime UltimaInteracaoEm { get; init; }
    public string UltimaMensagem { get; init; } = string.Empty;
    public int NaoLidas { get; init; }
}
