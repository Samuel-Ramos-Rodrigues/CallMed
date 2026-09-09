using MKSANCrud.Models;
using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.ViewModels;

public sealed class AgendaExcecoesPageViewModel
{
    public IReadOnlyList<AgendaExcecao> Excecoes { get; init; } = Array.Empty<AgendaExcecao>();
    public IReadOnlyList<Disponibilidade> BloqueiosDoMedico { get; init; } = Array.Empty<Disponibilidade>();
    public int TotalAtivas => Excecoes.Count(x => x.Ativa);
    public int TotalEncaixes => Excecoes.Count(x => x.Ativa && x.Tipo == AgendaExcecaoTipo.Encaixe);
    public int TotalBloqueios => Excecoes.Count(x => x.Ativa && x.Tipo == AgendaExcecaoTipo.Bloqueio);
    public int TotalAusencias => Excecoes.Count(x => x.Ativa && x.Tipo == AgendaExcecaoTipo.Ausencia);
}
