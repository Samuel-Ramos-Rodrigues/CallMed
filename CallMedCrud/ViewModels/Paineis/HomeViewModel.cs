using MKSANCrud.Models;

namespace MKSANCrud.ViewModels;

public class HomeViewModel
{
    public bool Autenticado { get; set; }
    public string NomePaciente { get; set; } = "Paciente";
    public int? PacienteId { get; set; }
    public string Convenio { get; set; } = "Particular";
    public List<Consulta> ProximasConsultas { get; set; } = new();
    public List<SolicitacaoAtendimento> SolicitacoesRecentes { get; set; } = new();
}
