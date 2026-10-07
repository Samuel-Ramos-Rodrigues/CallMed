# CallMed V21.6.3 — Identidade segura + refatoração visual

## Segurança do atendimento por IA

- Contatos externos sem `PacienteId` continuam podendo consultar médicos, especialidades e disponibilidade, mas não podem executar ações pessoais ou mutações.
- A trava é aplicada no backend antes das tools de:
  - agendamento;
  - confirmação;
  - remarcação;
  - cancelamento;
  - lista de espera;
  - consulta de dados pessoais.
- Nova tool `identificar_paciente` valida **CPF + data de nascimento** e, somente quando ambos correspondem a um paciente ativo real, vincula a conversa ao cadastro.
- O `ConversaAtendimentoId` é inserido pelo próprio backend no contexto do agente; o modelo não escolhe a conversa que será vinculada.
- Depois da identificação, a operação sensível só continua na mensagem seguinte, quando o contexto já foi recarregado com o paciente vinculado.
- O agente recebe uma resposta determinística quando uma ação exige identificação, evitando que o modelo declare agendamento concluído sem paciente conhecido.
- Confirmações naturais como `agende`, `marque` e `faça` passam a ser entendidas como confirmação explícita quando existe uma ação válida pendente.
- Sucessos de agendamento/remarcação/confirmação/cancelamento passam a ter confirmação final produzida a partir do resultado real da tool.

## Central de Atendimento

- Conversas sem paciente exibem alerta visual claro informando que alterações estão bloqueadas.
- O polling monitora alteração de `PacienteId`; quando a conversa é vinculada, a tela recarrega o nome e contexto do paciente automaticamente.

## Refatoração visual global

- Novo design system em `wwwroot/css/design-v2163.css`.
- Redesign de sidebar, topbar, cards, KPIs, tabelas, formulários, botões, estados e responsividade.
- Central de Atendimento recebeu novo espaçamento, hierarquia, mensagens, filtros, painel de contexto e melhor comportamento em tema escuro.
- Área do paciente, páginas administrativas, login/cadastro e site público usam a mesma linguagem visual.
- Tema claro/escuro preservado.
- PWA inclui o novo CSS no cache estático e usa novo nome de cache para evitar CSS antigo após deploy.

## Android / PWA

- `versionCode`: `2163`
- `versionName`: `21.6.3`
- cache do Service Worker: `callmed-static-v21-6-3-identity-design`
