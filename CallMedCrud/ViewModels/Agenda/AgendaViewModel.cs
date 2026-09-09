using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class AgendaViewModel
{
    public DateTime DataReferencia { get; init; }
    public DateTime InicioPeriodo { get; init; }
    public DateTime FimPeriodo { get; init; }
    public string Modo { get; init; } = "semana";
    public int? MedicoId { get; init; }
    public int? EspecialidadeId { get; init; }
    public IReadOnlyList<Medico> Medicos { get; init; } = Array.Empty<Medico>();
    public IReadOnlyList<Especialidade> Especialidades { get; init; } = Array.Empty<Especialidade>();
    public IReadOnlyList<AgendaCalendarioDiaViewModel> Dias { get; init; } = Array.Empty<AgendaCalendarioDiaViewModel>();
    public int TotalConsultas => Dias.Sum(d => d.Slots.Count(s => s.ConsultaId.HasValue));
    public int TotalLivres => Dias.Sum(d => d.Slots.Count(s => s.Ativo && !s.ConsultaId.HasValue));
    public int TotalBloqueados => Dias.Sum(d => d.Slots.Count(s => !s.Ativo && !s.ConsultaId.HasValue));
}
