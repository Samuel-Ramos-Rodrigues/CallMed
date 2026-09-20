using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.ViewModels;

public sealed class PacienteOpcaoAtendimentoViewModel
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Telefone { get; init; }
    public bool PossuiContaWeb { get; init; }
}
