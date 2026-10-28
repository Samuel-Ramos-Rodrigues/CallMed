using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class AcessosViewModel
{
    public List<Medico> Medicos { get; init; } = new();
    public List<Funcionario> Atendentes { get; init; } = new();
}
