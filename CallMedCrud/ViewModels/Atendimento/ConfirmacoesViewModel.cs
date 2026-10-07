namespace MKSANCrud.ViewModels;

public sealed class ConfirmacoesViewModel
{
    public string Papel { get; set; } = string.Empty;
    public bool Resumido { get; set; }
    public List<ConfirmacaoItemViewModel> Itens { get; set; } = [];
    public int Total => Itens.Count;
}
