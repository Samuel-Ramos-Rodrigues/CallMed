using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.ViewModels;

public sealed class ConveniosRegrasViewModel
{
    public IReadOnlyList<ConvenioEspecialidade> Regras { get; init; } = Array.Empty<ConvenioEspecialidade>();
    public IReadOnlyList<string> ConveniosCadastrados { get; init; } = Array.Empty<string>();
}
