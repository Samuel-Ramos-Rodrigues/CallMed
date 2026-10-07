using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.ViewModels;

public sealed class AuditoriaPageViewModel
{
    public IReadOnlyList<AuditoriaEvento> Itens { get; init; } = Array.Empty<AuditoriaEvento>();
    public string? Busca { get; init; }
    public string? Entidade { get; init; }
}
