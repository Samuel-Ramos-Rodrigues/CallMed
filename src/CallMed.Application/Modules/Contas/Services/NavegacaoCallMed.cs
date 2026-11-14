using System.Security.Claims;
namespace MKSANCrud.Services.Usuarios;
public sealed record ItemNavegacao(string Grupo,string Titulo,string Descricao,string Controller,string Action="Index");
public static class NavegacaoCallMed {
 public static ItemNavegacao[] Itens(ClaimsPrincipal u) {
  bool admin=u.IsInRole("Admin"),equipe=admin||u.IsInRole("Funcionario");
  if(equipe){var itens=new List<ItemNavegacao>{
   new("Agenda e vagas","Agenda","Ver horários, marcar e remarcar consultas.","Agenda"),
   new("Hoje","Pendências da recepção","Resolver exceções de check-in, pré-cadastro e teleconsultas.","RecepcaoPendencias"),
   new("Hoje","QR Code de check-in","Exibir código temporário de autoatendimento presencial.","Checkin","Terminal"),
   new("Hoje","Jornada das consultas","Acompanhar etapas e horários.","Jornada"),
   new("Atendimento","Mensagens","Conversar pelos canais e acompanhar a assistente.","Atendimento"),
   new("Agenda e vagas","Consultas","Acompanhar agendamentos, confirmações e histórico.","Consulta"),
   new("Atendimento","Solicitações","Organizar pedidos que ainda precisam de um horário.","Solicitacoes"),
   new("Agenda e vagas","Fila de espera","Acompanhar pacientes que aguardam uma vaga.","ListaEspera"),
   new("Pessoas e documentos","Pacientes","Dados de identificação e contato dos pacientes.","Paciente"),
   new("Pessoas e documentos","Profissionais","Profissão, especialidade, conselho e agenda semanal.","Medico"),
   new("Consultas e teleconsulta","Salas físicas e virtuais","Reservas físicas, manutenção e acompanhamento das consultas online.","Salas"),
   new("Agenda e vagas","Disponibilidade semanal","Configurar os horários em que cada profissional atende.","Disponibilidades"),
   new("Agenda e vagas","Bloqueios e exceções","Ajustar férias, ausências e alterações pontuais da agenda.","Disponibilidade"),
   new("Pessoas e documentos","Exames recebidos","Conferir documentos de exames enviados pelos pacientes.","HistoricoExames","Pendentes"),
   new("Indicadores e gestão","Relatórios","Consultar os resultados administrativos da clínica.","Relatorios"),
   new("Indicadores e gestão","Indicadores do atendimento","Faltas, espera, satisfação e execução das rotinas.","Operacao"),
   new("Indicadores e gestão","Comparação do piloto","Comparar faltas, ocupação e esforço antes/depois.","Piloto")};
   if(admin)itens.AddRange([
    new("Pessoas e documentos","Especialidades","Manter as áreas de atendimento da clínica.","Especialidade"),new("Pessoas e documentos","Equipe da recepção","Manter o cadastro administrativo.","Funcionario"),
    new("Administração","Acessos e permissões","Criar contas e definir os perfis da equipe.","Acessos"),new("Administração","Convênios","Conferir cobertura e regras administrativas.","Convenios"),
    new("Administração","Canais e integrações","Configurar os provedores de comunicação.","Integracoes"),new("Administração","Prontuário externo","Configurar o intercâmbio administrativo com o PEP.","Psicologia","Pep"),
    new("Administração","Preferências da clínica","Ajustar as opções do sistema.","Configuracoes"),new("Indicadores e gestão","Auditoria","Consultar os eventos de acesso e alterações.","Auditoria"),new("Indicadores e gestão","Pedidos de privacidade","Responder às solicitações autenticadas dos titulares.","Privacidade","Gestao")]);
   return itens.ToArray();
  }
  if(u.IsProfissionalSaude())return [new("Meu atendimento","Minha agenda","Ver as consultas sob minha responsabilidade.","MedicoPainel"),new("Meu atendimento","Salas dos meus atendimentos","Ver minhas reservas e acompanhar consultas por vídeo.","Salas"),new("Meu atendimento","Atendimentos","Preparação, chegada, sala online e conclusão das consultas.","Jornada"),new("Minha conta","Perfil e preferências","Atualizar meus dados e opções de acessibilidade.","MinhaConta")];
  return [new("Meu cuidado","Agendar consulta","Escolher especialidade, profissional, data e horário.","Consulta","Create"),new("Meu cuidado","Minhas consultas","Confirmar, preparar, remarcar ou cancelar uma consulta.","Consulta"),new("Meu cuidado","Fila de espera","Aguardar e aceitar uma oferta de horário.","ListaEspera"),new("Documentos","Meus exames","Consultar os exames associados à minha conta.","HistoricoExames"),new("Minha conta","Perfil e preferências","Atualizar dados, comunicação e acessibilidade.","MinhaConta"),new("Minha conta","Dados e privacidade","Acompanhar pedidos sobre meus dados pessoais.","Privacidade")];
 }
 // Navegação objetiva: as funções especializadas continuam no diretório de áreas.
 // Destaques da demanda SENAI: até cinco destinos visíveis; demais funções no diretório.
 // Quatro ou cinco destinos frequentes por perfil; catalogo acessivel em "Mais funções".
 public static ItemNavegacao[] Principais(ClaimsPrincipal u) =>
  u.IsInRole("Admin") ?
  [new("", "Visão geral", "", "FuncionarioPainel"),
   new("", "Operação", "", "RecepcaoPendencias"),
   new("", "Teleconsultas", "", "Salas"),
   new("", "Agenda", "", "Agenda"),
   new("", "Indicadores", "", "Operacao")] :
  u.IsInRole("Funcionario") ?
  [new("", "Hoje", "", "FuncionarioPainel"),
   new("", "Pendências", "", "RecepcaoPendencias"),
   new("", "Agenda", "", "Agenda"),
   new("", "Atendimento", "", "Atendimento"),
   new("", "Teleconsultas", "", "Salas")] :
  u.IsProfissionalSaude() ?
  [new("", "Meu dia", "", "MedicoPainel"),
   new("", "Consultas", "", "Jornada"),
   new("", "Teleconsulta", "", "Salas"),
   new("", "Meu perfil", "", "MinhaConta")] :
  [new("", "Início", "", "Home"),
   new("", "Consultas", "", "Consulta"),
   new("", "Agendar", "", "Consulta", "Create"),
   new("", "Teleconsulta", "", "Jornada")];
}
