using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public class MedicoPainelViewModel
{
    public Medico Medico { get; set; } = new();
    public int ConsultasHoje { get; set; }
    public int ConsultasSemana { get; set; }
    public int PacientesHoje { get; set; }
    public int VagasHoje { get; set; }
    public List<Consulta> AgendaHoje { get; set; } = new();
    public List<Consulta> ProximasConsultas { get; set; } = new();
    public List<MedicoHorarioSemanal> AgendaSemanal { get; set; } = new();
    public List<Disponibilidade> ProximasVagasLivres { get; set; } = new();
    public List<Disponibilidade> BloqueiosManuais { get; set; } = new();
}
