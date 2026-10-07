using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.ViewModels;

public sealed class TriagemSolicitacaoViewModel
{
    public SolicitacaoAtendimento Solicitacao { get; init; } = new();
    public ResultadoElegibilidadeConvenio? Elegibilidade { get; init; }
    public IReadOnlyList<AuditoriaEvento> Historico { get; init; } = Array.Empty<AuditoriaEvento>();
}
