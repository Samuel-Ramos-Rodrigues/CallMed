using System.Security.Claims;

namespace MKSANCrud.Services.Usuarios;

/// <summary>
/// Navegação por tarefa, não por entidade de banco. Mantém todas as rotas existentes
/// e limita a barra superior a atividades frequentes de cada perfil.
/// </summary>
public sealed record ItemNavegacao(
    string Grupo,
    string Titulo,
    string Descricao,
    string Controller,
    string Action = "Index");

public static class NavegacaoCallMed
{
    public static ItemNavegacao[] Itens(ClaimsPrincipal usuario)
    {
        var admin = usuario.IsInRole("Admin");
        var equipe = admin || usuario.IsInRole("Funcionario");

        if (equipe)
        {
            var itens = new List<ItemNavegacao>
            {
                // 1. Trabalho do dia: a equipe resolve exceções, não repete automações.
                new("Hoje", "Pendências que exigem ajuda", "Check-in, cadastro e teleconsulta que precisam da equipe.", "RecepcaoPendencias"),
                new("Hoje", "QR Code da recepção", "Autoatendimento presencial com QR temporário.", "Checkin", "Terminal"),
                new("Hoje", "Jornada das consultas", "Preparação, chegada, andamento e conclusão.", "Jornada"),

                // 2. Uma só área para agendamento, confirmações, vagas e encaixes.
                new("Agenda inteligente", "Agenda geral", "Consultar horários e marcar ou remarcar.", "Agenda"),
                new("Agenda inteligente", "Consultas", "Histórico, confirmação e situação dos agendamentos.", "Consulta"),
                new("Agenda inteligente", "Confirmações", "Acompanhar respostas e exceções dos lembretes.", "Confirmacoes"),
                new("Agenda inteligente", "Fila de espera e encaixes", "Acompanhar ofertas automáticas de vagas.", "ListaEspera"),
                new("Agenda inteligente", "Disponibilidade semanal", "Horários regulares dos profissionais.", "Disponibilidades"),
                new("Agenda inteligente", "Bloqueios e exceções", "Férias, ausências e alterações pontuais.", "Disponibilidade"),

                // 3. Canais e solicitações no mesmo fluxo operacional.
                new("Atendimento", "Central de Atendimento", "E-mail, WhatsApp, SMS, IA e atendentes.", "Atendimento"),
                new("Atendimento", "Solicitações", "Pedidos que precisam ser direcionados para uma vaga.", "Solicitacoes"),

                // 4. Teleconsulta permanece uma capacidade visível, não escondida em salas.
                new("Teleconsulta", "Consultas virtuais", "Acompanhar presença, sala e acesso online.", "Salas"),

                // 5. Cadastros e documentos acessíveis sem poluir o menu principal.
                new("Pessoas e documentos", "Pacientes", "Dados cadastrais e contatos.", "Paciente"),
                new("Pessoas e documentos", "Profissionais", "Especialidades, conselho e agenda.", "Medico"),
                new("Pessoas e documentos", "Exames recebidos", "Documentos enviados para conferência.", "HistoricoExames", "Pendentes"),

                // 6. Indicadores de resultado, não da rotina de recepção.
                new("Indicadores", "Resultados operacionais", "Faltas, espera, satisfação e execução das rotinas.", "Operacao"),
                new("Indicadores", "Relatórios", "Relatórios administrativos.", "Relatorios"),
                new("Indicadores", "Comparação do piloto", "Indicadores antes e depois da implantação.", "Piloto"),
            };

            if (admin)
            {
                itens.AddRange(
                [
                    new("Gestão da clínica", "Especialidades", "Cadastro das áreas atendidas.", "Especialidade"),
                    new("Gestão da clínica", "Equipe", "Funcionários e profissionais de recepção.", "Funcionario"),
                    new("Gestão da clínica", "Acessos e permissões", "Gerenciar contas e perfis autorizados.", "Acessos"),
                    new("Gestão da clínica", "Convênios", "Cobertura e regras de pagamento.", "Convenios"),
                    new("Gestão da clínica", "Canais e integrações", "Gmail, WhatsApp, httpSMS e provedores.", "Integracoes"),
                    new("Gestão da clínica", "Prontuário externo", "Integração administrativa com PEP.", "Psicologia", "Pep"),
                    new("Gestão da clínica", "Preferências da clínica", "Opções e parâmetros operacionais.", "Configuracoes"),
                    new("Gestão da clínica", "Auditoria", "Registros de acesso e alterações.", "Auditoria"),
                    new("Gestão da clínica", "Privacidade", "Pedidos dos titulares de dados.", "Privacidade", "Gestao")
                ]);
            }

            return itens.ToArray();
        }

        if (usuario.IsProfissionalSaude())
        {
            return
            [
                new("Meu dia", "Minha agenda", "Próximos pacientes e ações da consulta.", "MedicoPainel"),
                new("Meu dia", "Acompanhar consultas", "Preparação, chegada e conclusão.", "Jornada"),
                new("Teleconsulta", "Minha sala online", "Reservas e acesso aos atendimentos por vídeo.", "Salas"),
                new("Minha conta", "Perfil e preferências", "Dados, conta e acessibilidade.", "MinhaConta")
            ];
        }

        return
        [
            new("Meu cuidado", "Agendar consulta", "Escolher especialidade, profissional, data e hora.", "Consulta", "Create"),
            new("Meu cuidado", "Minhas consultas", "Confirmar, preparar, remarcar ou cancelar.", "Consulta"),
            new("Meu cuidado", "Preparação e check-in", "Acompanhar as etapas da consulta.", "Jornada"),
            new("Meu cuidado", "Fila de espera", "Aguardar e aceitar uma oferta de horário.", "ListaEspera"),
            new("Teleconsulta", "Minhas consultas online", "Acessar a preparação e a sala autorizada.", "Jornada"),
            new("Documentos", "Meus exames", "Histórico de exames da conta.", "HistoricoExames"),
            new("Minha conta", "Meu perfil", "Dados, contato e preferências.", "MinhaConta"),
            new("Minha conta", "Dados e privacidade", "Exercício de direitos sobre os dados pessoais.", "Privacidade")
        ];
    }

    public static ItemNavegacao[] Principais(ClaimsPrincipal usuario) =>
        usuario.IsInRole("Admin") ?
        [
            new("", "Visão geral", "", "FuncionarioPainel"),
            new("", "Hoje", "", "RecepcaoPendencias"),
            new("", "Agenda", "", "Agenda"),
            new("", "Teleconsultas", "", "Salas"),
            new("", "Gestão", "", "Organizacao")
        ] :
        usuario.IsInRole("Funcionario") ?
        [
            new("", "Hoje", "", "FuncionarioPainel"),
            new("", "Agenda", "", "Agenda"),
            new("", "Atendimento", "", "Atendimento"),
            new("", "Pendências", "", "RecepcaoPendencias"),
            new("", "Teleconsultas", "", "Salas")
        ] :
        usuario.IsProfissionalSaude() ?
        [
            new("", "Meu dia", "", "MedicoPainel"),
            new("", "Consultas", "", "Jornada"),
            new("", "Teleconsulta", "", "Salas"),
            new("", "Meu perfil", "", "MinhaConta")
        ] :
        [
            new("", "Início", "", "Home"),
            new("", "Agendar", "", "Consulta", "Create"),
            new("", "Consultas", "", "Consulta"),
            new("", "Teleconsulta", "", "Jornada")
        ];
}
