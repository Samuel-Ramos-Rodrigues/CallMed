using System.ComponentModel.DataAnnotations;
using MKSANCrud.Data;

namespace MKSANCrud.Models;

public class AuditoriaEvento
{
    public long Id { get; set; }

    public string? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    [StringLength(180)]
    public string? UsuarioNome { get; set; }

    [Required]
    [StringLength(80)]
    public string Acao { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Entidade { get; set; } = string.Empty;

    [StringLength(80)]
    public string? EntidadeId { get; set; }

    [StringLength(1200)]
    public string? Descricao { get; set; }

    [StringLength(3000)]
    public string? ValorAnterior { get; set; }

    [StringLength(3000)]
    public string? ValorNovo { get; set; }

    [StringLength(64)]
    public string? Ip { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
