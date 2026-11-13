namespace MKSANCrud.ViewModels;

public sealed class MenuContaViewModel
{
    public string Nome { get; init; } = "Minha conta";
    public string Papel { get; init; } = "Minha conta";
    public string Email { get; init; } = "";
    public Guid? FotoVersao { get; init; }
    public string Iniciais => string.Join("", Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(p => p[..1].ToUpperInvariant()));
}
