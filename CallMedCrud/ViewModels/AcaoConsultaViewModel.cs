using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class AcaoConsultaViewModel
{
    public string Token { get; init; } = string.Empty;
    public string Acao { get; init; } = string.Empty;
    public Consulta? Consulta { get; init; }
    public Disponibilidade? Vaga { get; init; }
    public PreConsultaFormulario? Formulario { get; init; }
    public IReadOnlyList<Disponibilidade> Vagas { get; init; } = [];
    public string? UrlPortal { get; init; }
}
