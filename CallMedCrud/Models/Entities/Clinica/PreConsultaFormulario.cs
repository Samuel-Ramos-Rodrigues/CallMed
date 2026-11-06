using System.ComponentModel.DataAnnotations;

namespace MKSANCrud.Models;

// Pré-cadastro e anamnese simplificada, enviados ao paciente antes da consulta.
public class PreConsultaFormulario
{
    public int Id { get; set; }
    public int ConsultaId { get; set; }

    [StringLength(500)]
    public string? QueixaPrincipal { get; set; }

    [StringLength(500)]
    public string? Alergias { get; set; }

    [StringLength(500)]
    public string? MedicamentosEmUso { get; set; }

    [StringLength(1000)]
    public string? HistoricoRelevante { get; set; }

    [StringLength(120)]
    public string? ConvenioNome { get; set; }

    [StringLength(60)]
    public string? ConvenioCarteirinha { get; set; }

    [StringLength(20)]
    public string FormaPagamento { get; set; } = "Particular";

    public DateTime PreenchidoEm { get; set; } = DateTime.UtcNow;
}
