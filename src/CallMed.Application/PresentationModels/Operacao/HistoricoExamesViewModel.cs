using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class HistoricoExamesViewModel
{
    public Paciente Paciente { get; set; } = null!;
    public IReadOnlyList<ExameHistorico> Exames { get; set; } = [];
    public ExameHistorico Novo { get; set; } = new();
}
