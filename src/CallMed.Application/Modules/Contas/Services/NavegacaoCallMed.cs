using System.Security.Claims;
namespace MKSANCrud.Services.Usuarios;
public sealed record ItemNavegacao(string Grupo,string Titulo,string Descricao,string Controller,string Action="Index");
public static class NavegacaoCallMed {
 public static ItemNavegacao[] Itens(ClaimsPrincipal u) {
  bool admin=u.IsInRole("Admin"),equipe=admin||u.IsInRole("Funcionario");
  if(equipe){var itens=new List<ItemNavegacao>{
   new("Rotina","Agenda","Ver horários, marcar e remarcar consultas.","Agenda"),
   new("Rotina","Recepção do dia","Validar chegada, conferir preparo, organizar salas e acompanhar o atendimento.","Jornada"),
   new("Rotina","Mensagens","Conversar pelos canais e acompanhar a assistente.","Atendimento"),
   new("Rotina","Consultas","Acompanhar agendamentos, confirmações e histórico.","Consulta"),
   new("Rotina","Solicitações","Organizar pedidos que ainda precisam de um horário.","Solicitacoes"),
   new("Rotina","Fila de espera","Acompanhar pacientes que aguardam uma vaga.","ListaEspera"),
   new("Cadastros","Pacientes","Dados de identificação e contato dos pacientes.","Paciente"),
   new("Cadastros","Profissionais","Profissão, especialidade, conselho e agenda semanal.","Medico"),
   new("Agenda e recursos","Salas físicas e virtuais","Reservas físicas, manutenção e acompanhamento das consultas online.","Salas"),
   new("Agenda e recursos","Disponibilidade semanal","Configurar os horários em que cada profissional atende.","Disponibilidades"),
   new("Agenda e recursos","Bloqueios e exceções","Ajustar férias, ausências e alterações pontuais da agenda.","Disponibilidade"),
   new("Rotina","Exames recebidos","Conferir documentos de exames enviados pelos pacientes.","HistoricoExames","Pendentes"),
   new("Gestão","Relatórios","Consultar os resultados administrativos da clínica.","Relatorios"),
   new("Gestão","Indicadores do atendimento","Faltas, espera, satisfação e execução das rotinas.","Operacao"),
   new("Gestão","Comparação do piloto","Comparar faltas, ocupação e esforço antes/depois.","Piloto")};
   if(admin)itens.AddRange([
    new("Cadastros","Especialidades","Manter as áreas de atendimento da clínica.","Especialidade"),new("Cadastros","Equipe da recepção","Manter o cadastro administrativo.","Funcionario"),
    new("Configuração","Acessos e permissões","Criar contas e definir os perfis da equipe.","Acessos"),new("Configuração","Convênios","Conferir cobertura e regras administrativas.","Convenios"),
    new("Configuração","Canais e integrações","Configurar os provedores de comunicação.","Integracoes"),new("Configuração","Prontuário externo","Configurar o intercâmbio administrativo com o PEP.","Psicologia","Pep"),
    new("Configuração","Preferências da clínica","Ajustar as opções do sistema.","Configuracoes"),new("Gestão","Auditoria","Consultar os eventos de acesso e alterações.","Auditoria"),new("Gestão","Pedidos de privacidade","Responder às solicitações autenticadas dos titulares.","Privacidade","Gestao")]);
   return itens.ToArray();
  }
  if(u.IsProfissionalSaude())return [new("Meu atendimento","Minha agenda","Ver as consultas sob minha responsabilidade.","MedicoPainel"),new("Meu atendimento","Salas dos meus atendimentos","Ver minhas reservas e acompanhar consultas por vídeo.","Salas"),new("Meu atendimento","Atendimentos","Preparação, chegada, sala online e conclusão das consultas.","Jornada"),new("Minha conta","Perfil e preferências","Atualizar meus dados e opções de acessibilidade.","MinhaConta")];
  return [new("Meu cuidado","Agendar consulta","Escolher especialidade, profissional, data e horário.","Consulta","Create"),new("Meu cuidado","Minhas consultas","Confirmar, preparar, remarcar ou cancelar uma consulta.","Consulta"),new("Meu cuidado","Fila de espera","Aguardar e aceitar uma oferta de horário.","ListaEspera"),new("Documentos","Meus exames","Consultar os exames associados à minha conta.","HistoricoExames"),new("Minha conta","Perfil e preferências","Atualizar dados, comunicação e acessibilidade.","MinhaConta"),new("Minha conta","Dados e privacidade","Acompanhar pedidos sobre meus dados pessoais.","Privacidade")];
 }
 // Navegação objetiva: as funções especializadas continuam no diretório de áreas.
 // Destaques da demanda SENAI: até cinco destinos visíveis; demais funções no diretório.
 public static ItemNavegacao[] Principais(ClaimsPrincipal u) => u.IsInRole("Admin") || u.IsInRole("Funcionario") ?
  [new("", "Início", "", "FuncionarioPainel"),
   new("", "Agenda", "", "Agenda"),
   new("", "Teleconsultas", "", "Salas"),
   new("", "Atendimento", "", "Atendimento"),
   new("", "Check-in", "", "Jornada")] : u.IsProfissionalSaude() ?
  [new("", "Minha agenda", "", "MedicoPainel"),
   new("", "Teleconsultas", "", "Salas"),
   new("", "Atendimentos", "", "Jornada"),
   new("", "Meu perfil", "", "MinhaConta")] :
  [new("", "Início", "", "Home"),
   new("", "Agendar", "", "Consulta", "Create"),
   new("", "Teleconsulta", "", "Jornada"),
   new("", "Minhas consultas", "", "Consulta")];
}
