using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

public class Disponibilidade
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe a data da disponibilidade.")]
    [DataType(DataType.Date)]
    public DateTime? Data { get; set; }

    [Required(ErrorMessage = "Informe o horário da disponibilidade.")]
    [StringLength(8)]
    public string Horario { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public bool OrigemAgendaSemanal { get; set; }

    /// <summary>Bloqueio criado manualmente pelo médico; a renovação da agenda semanal não reabre este slot.</summary>
    public bool BloqueioManual { get; set; }

    public int? AgendaExcecaoId { get; set; }
    public AgendaExcecao? AgendaExcecao { get; set; }

    [Required(ErrorMessage = "Selecione o médico.")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o médico.")]
    public int MedicoId { get; set; }
    public Medico? Medico { get; set; }
}
