using System.ComponentModel.DataAnnotations;
namespace MKSANCrud.Models;

// Cada correção é uma nova revisão; o agregado usa somente a última do dia/período.
public sealed class MedicaoPiloto {
 public long Id {get;set;}
 public DateTime Data {get;set;}
 [MaxLength(10)] public string Periodo {get;set;} = "Piloto";
 public int Revisao {get;set;}
 public bool Desconsiderada {get;set;}
 public int Realizadas {get;set;}
 public int Faltas {get;set;}
 public int Canceladas {get;set;}
 public int SemDesfecho {get;set;}
 public int? MinutosAgendados {get;set;}
 public int? MinutosDisponiveis {get;set;}
 public int? MinutosRecepcao {get;set;}
 public decimal? CustoHoraRecepcao {get;set;}
 [MaxLength(300)] public string Fonte {get;set;} = "";
 [MaxLength(300)] public string Motivo {get;set;} = "";
 [MaxLength(64)] public string? AgendaHash {get;set;}
 public DateTime RegistradoEm {get;set;} = DateTime.UtcNow;
 public string RegistradoPor {get;set;} = "";
 public int Consultas => Realizadas+Faltas+Canceladas+SemDesfecho;
}
