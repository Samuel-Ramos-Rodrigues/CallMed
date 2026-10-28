using System.ComponentModel.DataAnnotations;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.ViewModels;

public sealed class SolicitacoesPainelViewModel
{
    public IReadOnlyList<SolicitacaoAtendimento> Itens { get; init; } = Array.Empty<SolicitacaoAtendimento>();
    public string? Busca { get; init; }
    public string? Canal { get; init; }
    public int TotalNovas => Itens.Count(x => x.Status == StatusSolicitacaoAtendimento.Nova);
    public int TotalTriagem => Itens.Count(x => x.Status == StatusSolicitacaoAtendimento.EmTriagem);
    public int TotalBuscando => Itens.Count(x => x.Status == StatusSolicitacaoAtendimento.BuscandoHorario);
    public int TotalAguardando => Itens.Count(x => x.Status == StatusSolicitacaoAtendimento.AguardandoPaciente);
    public int TotalConfirmadas => Itens.Count(x => x.Status == StatusSolicitacaoAtendimento.Confirmada);
}
