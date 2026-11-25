namespace MKSANCrud.Models;
public sealed class BloqueioSala {
 public int Id {get;set;} public int SalaId {get;set;} public SalaAtendimento Sala {get;set;}=null!;
 public DateTime Inicio {get;set;} public DateTime Fim {get;set;} public string Motivo {get;set;}="";
 public DateTime CriadoEm {get;set;}=DateTime.UtcNow; public DateTime? CanceladoEm {get;set;}
}
/// <summary>Somente metadados de presença; não armazena áudio, vídeo ou SDP.</summary>
public sealed class SessaoTele {
 public Guid Id {get;set;}=Guid.NewGuid(); public int ConsultaId {get;set;} public string UsuarioId {get;set;}="";
 public string Papel {get;set;}=""; public string Snapshot {get;set;}=""; public string Estado {get;set;}="aguardando";
 public DateTime EntradaEm {get;set;}=DateTime.UtcNow; public DateTime UltimoSinalEm {get;set;}=DateTime.UtcNow;
 public DateTime? ConectadaEm {get;set;} public DateTime? SaidaEm {get;set;} public string? MotivoSaida {get;set;}
}
