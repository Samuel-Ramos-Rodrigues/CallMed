namespace MKSANCrud.Models;
// O corpo e a identificação clínica ficam dentro do envelope cifrado pelo navegador.
public sealed class RegistroPsicologico {
 public Guid Id {get;set;} = Guid.NewGuid();
 public int ConsultaId {get;set;}
 public int PacienteId {get;set;}
 public int PsicologoId {get;set;}
 public string AutorUsuarioId {get;set;} = "";
 public string Escopo {get;set;} = "Prontuario";
 public string Categoria {get;set;} = "Evolucao";
 public string Envelope {get;set;} = "";
 public string Hash {get;set;} = "";
 public Guid? RetificaId {get;set;}
 public DateTime CriadoEm {get;set;} = DateTime.UtcNow;
}
public sealed class AutorizacaoResponsavel {
 public Guid Id {get;set;} = Guid.NewGuid();
 public int PacienteId {get;set;}
 public int PsicologoId {get;set;}
 public string Nome {get;set;} = "";
 public string Vinculo {get;set;} = "";
 public string Referencia {get;set;} = "";
 public string VerificadaPor {get;set;} = "";
 public DateTime VerificadaEm {get;set;} = DateTime.UtcNow;
 public DateTime ValidaAte {get;set;}
 public DateTime? RevogadaEm {get;set;}
}

public sealed class GuardaRegistroPsicologico {
 public Guid Id {get;set;} = Guid.NewGuid();
 public Guid RegistroId {get;set;}
 public int PsicologoId {get;set;}
 public Guid ChaveId {get;set;}
 public string ChaveCifrada {get;set;} = "";
 public string ConcedidaPor {get;set;} = "";
 public DateTime ConcedidaEm {get;set;} = DateTime.UtcNow;
 public RegistroPsicologico Registro {get;set;} = null!;
}
