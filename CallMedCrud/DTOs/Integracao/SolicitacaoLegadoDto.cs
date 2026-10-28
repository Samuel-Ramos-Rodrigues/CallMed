namespace MKSANCrud.DTOs.Integracao;

public sealed class SolicitacaoLegadoDto
{
    public string Canal { get; set; } = "Web";
    public int? PacienteId { get; set; }
    public int? EspecialidadeId { get; set; }
    public int? MedicoId { get; set; }
    public DateTime? DataPreferida { get; set; }
    public string? Periodo { get; set; }
    public string? Observacao { get; set; }
    public string? NomeContato { get; set; }
    public string? TelefoneContato { get; set; }
    public string? EmailContato { get; set; }
}
