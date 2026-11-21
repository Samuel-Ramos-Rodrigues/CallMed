using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;
public static class ConsentimentoAtendimento {
 public static string Versao(Consulta c)=>c.Medico?.EhPsicologia!=false?ConsentimentoPsicologia.Versao:"atendimento-video-v1";
 public static string Texto(Consulta c)=>c.Medico?.EhPsicologia!=false?ConsentimentoPsicologia.Texto:
  "A consulta online ocorre entre o paciente e o profissional responsável identificado no agendamento. Autorizo a transmissão de imagem e voz para esta consulta. O CallMed usa WebRTC com mídia cifrada entre participantes, podendo usar retransmissor TURN, e não grava a chamada. Cada participante deve buscar ambiente privativo. O profissional confirma a identidade e a adequação do atendimento remoto e pode combinar uma alternativa presencial em caso de interrupção. Este aceite não substitui os documentos e orientações específicos do atendimento. Em urgência, procure atendimento humano adequado.";
 public static string Hash(Consulta c)=>PsicologiaService.Hash(Texto(c));
}
