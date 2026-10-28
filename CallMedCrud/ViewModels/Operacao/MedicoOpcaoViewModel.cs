namespace MKSANCrud.ViewModels;

/// <summary>Dados mínimos do médico para selects filtrados por especialidade.</summary>
public sealed class MedicoOpcaoViewModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public int? EspecialidadeId { get; init; }
}
