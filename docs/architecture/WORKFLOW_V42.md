# CallMed V42 — Organização pela jornada de consultas

## Finalidade
Atender à demanda de redução de faltas e sobrecarga administrativa: agendamento, confirmação ativa, pré-cadastro, check-in, teleconsulta e encaixe automático com fila de espera. Separar **tarefas do usuário** de **automações de bastidor**.

## Cinco áreas da equipe
1. **Hoje:** recepção, exceções de check-in, QR e acompanhamento da jornada (o painel continua priorizando pendências reais).
2. **Agenda inteligente:** consultas, calendário, disponibilidade, confirmações/remarcações, fila de espera.
3. **Atendimento:** central de mensagens (E-mail/WhatsApp/SMS/IA) e solicitações.
4. **Teleconsulta:** salas e sessões online. Permanece destacada no menu.
5. **Gestão da clínica:** pessoas, documentos, indicadores, permissões, canais e configurações, visíveis segundo o perfil.

As funções originais e as permissões por papel permanecem. Os grupos acima reorganizam o catálogo, não transferem permissões ao front-end.

## Jornada unificada da consulta
- **Agendamento:** data, profissional e horário, registrados na entidade Consulta.
- **Confirmação:** status da consulta e ações existentes de confirmação; sem presumir que o check-in aconteceu.
- **Preparação e check-in:** acesso à ação `Jornada/Consulta/{id}`, que lê os eventos reais da Jornada.
- **Teleconsulta e realização:** são mostradas na Jornada, conforme modalidade e permissões.
- **Encerramento:** exibido somente quando o status realmente é Realizada/Cancelada/Ausente.

A view `Consulta/Details` ganhou um resumo do percurso com botão para a Jornada existente. A lista não grava novos estados, evitando divergências com o banco. A ação sempre passa pelas checagens atuais de autorização.

## Comportamentos automáticos já existentes, a confirmar em integração real
Confirmações/lembretes, fila de espera, QR/check-in, comunicação por canais e jobs de monitoramento. O painel de recepção reúne exceções para a equipe. **Esta PR não altera o motor de automação** nem afirma que todos esses fluxos estejam operando sem falhas na produção; isso exige testes ponta a ponta com contas/consultas de homologação.

## Próxima fase de produto (ainda não implementada)
- Exceções consolidadas em um único feed, com tipo, gravidade, vínculo à consulta, responsável e ação sugerida.
- Orquestrador idempotente para cancelamento → liberar vaga → ofertar à fila → aceitar no prazo → revalidar agenda → confirmar consulta; auditoria em cada transição.
- Visibilidade de execução de cada job, fila de mensagens e falhas de canais; alerta apenas quando houver algo a resolver.
- Testes de integração de quatro perfis, check-in presencial/online e cenários de aceitação/rejeição/expiração de ofertas.
- Revisão visual do calendário V41 (dependência desta branch) e validação responsiva dos 14 módulos CSS.
