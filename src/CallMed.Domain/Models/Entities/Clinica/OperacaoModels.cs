using System.ComponentModel.DataAnnotations;
namespace MKSANCrud.Models;
public sealed class SaudeRotina {
 [Key,MaxLength(80)] public string Nome {get;set;}="";
 public DateTime TentativaEm {get;set;}
 public DateTime? SucessoEm {get;set;}
 public DateTime? FalhaEm {get;set;}
 public int FalhasConsecutivas {get;set;}
}
public sealed class PedidoPrivacidade {
 public Guid Id {get;set;}=Guid.NewGuid();
 public string UsuarioId {get;set;}="";
 [MaxLength(30)] public string Tipo {get;set;}="";
 [MaxLength(30)] public string Estado {get;set;}="Recebido";
 public DateTime CriadoEm {get;set;}=DateTime.UtcNow;
 public DateTime? RespondidoEm {get;set;}
 public string? RespondidoPor {get;set;}
 public string? RespostaProtegida {get;set;}
 public Guid Versao {get;set;}=Guid.NewGuid();
}
