using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class ListaEspera
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public int? MedicoId { get; set; }
    public Medico? Medico { get; set; }

    public int? EspecialidadeId { get; set; }
    public Especialidade? Especialidade { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DataPreferida { get; set; }

    [StringLength(20)]
    public string Periodo { get; set; } = "Qualquer";

    [StringLength(500)]
    public string? Observacao { get; set; }

    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? NotificadoEm { get; set; }
    public int? UltimaDisponibilidadeId { get; set; }
}
