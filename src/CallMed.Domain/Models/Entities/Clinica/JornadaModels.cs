namespace MKSANCrud.Models;
public sealed class Jornada {
 public DateTime? PresencaConfirmadaEm {get;set;}
 public int ConsultaId {get;set;}
 public int? SalaId {get;set;}
 public int Duracao {get;set;} = 30;
 public bool Tele {get;set;}
 public bool PagamentoConferido {get;set;}
 public int Revisao {get;set;}
 public DateTime? CadastroEm {get;set;}
 public DateTime? CheckInEm {get;set;}
 public DateTime? ChegadaEm {get;set;}
 public DateTime? InicioEm {get;set;}
 public DateTime? FimEm {get;set;}
 public DateTime? ConsentimentoEm {get;set;}
 public string? ConsentimentoVersao {get;set;}
 public DateTime AtualizadoEm {get;set;} = DateTime.UtcNow;
 public Consulta Consulta {get;set;} = null!;
 public SalaAtendimento? Sala {get;set;}
}
public sealed class Oferta {
 public Guid Id {get;set;} = Guid.NewGuid();
 public int ListaEsperaId {get;set;}
 public int MedicoId {get;set;}
 public DateTime Data {get;set;}
 public string Horario {get;set;} = "";
 public string Estado {get;set;} = "Aberta";
 public DateTime ExpiraEm {get;set;}
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
 public DateTime? NotificadaEm {get;set;}
 public int? ConsultaId {get;set;}
 public int Tentativas {get;set;}
 public ListaEspera ListaEspera {get;set;} = null!;
}
public sealed class ChaveClinica {
 public DateTime? RevogadaEm {get;set;}
 public Guid Id {get;set;} = Guid.NewGuid();
 public string UsuarioId {get;set;} = "";
 public string Publica {get;set;} = "";
 public string Impressao {get;set;} = "";
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
}
public sealed class FormularioClinico {
 public int Id {get;set;}
 public int MedicoId {get;set;}
 public int Versao {get;set;}
 public string Titulo {get;set;} = "";
 public string Perguntas {get;set;} = "[]";
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
}
public sealed class RespostaClinica {
 public DateTime AutorizadaEm {get;set;} = DateTime.UtcNow;
 public Guid Id {get;set;} = Guid.NewGuid();
 public int ConsultaId {get;set;}
 public int FormularioId {get;set;}
 public int MedicoId {get;set;}
 public int PacienteId {get;set;}
 public string Envelope {get;set;} = "";
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
}
public sealed class SinalTele {
 public long Id {get;set;}
 public int ConsultaId {get;set;}
 public string UsuarioId {get;set;} = "";
 public string Protegido {get;set;} = "";
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
}
public sealed class LinkConsulta {
 public Guid Id {get;set;} = Guid.NewGuid();
 public int ConsultaId {get;set;}
 public string Snapshot {get;set;} = "";
 public DateTime ExpiraEm {get;set;}
 public DateTime? UsadoEm {get;set;}
}
public sealed class MobileTicket {
 public Guid Id {get;set;} = Guid.NewGuid();
 public int ConsultaId {get;set;}
 public string Principal {get;set;} = "";
 public DateTime ExpiraEm {get;set;}
 public bool Usado {get;set;}
}
public sealed class MedicaoOperacional {
 public int Id {get;set;}
 public DateTime Data {get;set;}
 public string Periodo {get;set;} = "Piloto";
 public int Consultas {get;set;}
 public int Faltas {get;set;}
 public int MinutosRecepcao {get;set;}
 public string Fonte {get;set;} = "";
}
public sealed class PepEvento {
 public DateTime? FhirEntregueEm {get;set;}
 public DateTime? FhirTentarEm {get;set;}
 public int FhirTentativas {get;set;}
 public int PacienteId {get;set;}
 public int MedicoId {get;set;}
 public Guid Id {get;set;} = Guid.NewGuid();
 public int ConsultaId {get;set;}
 public string Estado {get;set;} = "";
 public DateTime Data {get;set;}
 public string Horario {get;set;} = "";
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
 public DateTime? EntregueEm {get;set;}
 public DateTime? TentarEm {get;set;}
 public int Tentativas {get;set;}
 public Consulta Consulta {get;set;} = null!;
}
