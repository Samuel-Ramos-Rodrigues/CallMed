namespace MKSANCrud.ViewModels;

public sealed class ConfirmacaoItemViewModel
{
    public string Tipo { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Ordenacao { get; set; }
    public int? MedicoId { get; set; }
    public DateTime? Data { get; set; }
    public string? Horario { get; set; }
    public int? ListaEsperaId { get; set; }
}
