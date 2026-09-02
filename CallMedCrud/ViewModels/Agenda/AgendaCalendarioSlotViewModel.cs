using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class AgendaCalendarioSlotViewModel
{
    public int? DisponibilidadeId { get; init; }
    public int MedicoId { get; init; }
    public string MedicoNome { get; init; } = string.Empty;
    public string Especialidade { get; init; } = string.Empty;
    public string Horario { get; init; } = string.Empty;
    public bool Ativo { get; init; }
    public bool Encaixe { get; init; }
    public bool BloqueioManual { get; init; }
    public int? ConsultaId { get; init; }
    public string? PacienteNome { get; init; }
    public string? StatusConsulta { get; init; }
}
