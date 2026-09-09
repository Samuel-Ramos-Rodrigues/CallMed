using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public sealed class AgendaCalendarioDiaViewModel
{
    public DateTime Data { get; init; }
    public IReadOnlyList<AgendaCalendarioSlotViewModel> Slots { get; init; } = Array.Empty<AgendaCalendarioSlotViewModel>();
}
