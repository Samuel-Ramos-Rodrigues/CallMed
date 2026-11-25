# Roteiro de homologação R9.3.2 — checklist solicitado

A marca abaixo identifica **implementação/inspeção estática** no ZIP. O teste final **no navegador real** permanece obrigatório. **Não utilize dados médicos reais nos testes funcionais de gravação/envio.**

| # | Pedido | Implementação e tela | Verificação pós-deploy necessária |
|---|---|---|---|
| 1 | Guia 5 passos | CSS e `Views/Consulta/Create.cshtml` | Fundo escuro + texto claro, sem bloco cinza-branco |
| 2 | Cinco marcadores | `Views/Consulta/_Form.cshtml`, `data-state`, ARIA | Escolher especialidade, médico, data, horário e conferir os cinco estados |
| 3 | Tokens de cards | Overrides de R5 em CSS unificado | Percorrer todas as páginas dos quatro perfis, principalmente lista, agenda e salas |
| 4 | Textos auxiliares | Guia, placeholders, mensagens vazias | Contraste com alto contraste ativado/desativado |
| 5 | Botões, tabelas, alerts, selects, modais | Overrides em áreas da agenda e estilos preservados | Inspecionar formulários, alertas e modais realmente abertos |
| 6 | Reduzir competição do guia | Guia compacto sem lista duplicada | Confirmar prioridade visual do formulário |
| 7 | Avanço das escolhas | `updateSteps` e botão submit | Validar seleção, voltar etapa, mudar médico e não habilitar confirmação incorreta |
| 8 | Estados vazios e vagas | `renderCalendar`, `renderButtons`, CSS | Nenhum horário, falha temporária de API, data indisponível |
| 9 | Chat não cobrir conteúdo | JS `updateChatLayout`, CSS de dock/modal | Abrir em `/Consulta/Create` a 1440px e a 1024px, navegar no formulário |
| 10 | Chat contraste e fechamento | CSS widget, HTML, JS | Histórico longo, mensagens claras/escuras, cabeçalho, rodapé, botão fechar |
| 11 | Chat mobile | Media queries, backdrop, foco | 390px/768px, Escape, rolagem e teclado virtual |
| 12 | Gráfico vazio | `Views/FuncionarioPainel/Index.cshtml` | Filtro Hoje sem dados, mensagem legível no lugar da rosca |
| 13 | Datas e porcentagens | Controller R9.3.1 preservado | Conferir contra consultas de teste para cada período; sem expectativas inventadas |
| 14 | Períodos atualizam KPIs | Controller + select R9.3.1 preservados | Comparar KPIs, legendas e gráfico após aplicar filtro |
| 15 | Menus ativos | CSS dark da navegação + cabeçalhos | Cada perfil com item ativo visível e submenu sem sobreposição |
| 16 | Permissões | Correções de links da R9.3.1 mantidas | Seguir links de paciente, médico, atendente e admin, verificar autorizações |
| 17 | Cabeçalho e acessibilidade | CSS/JS já existentes, foco do chat reforçado | Abrir sino, acessibilidade, perfil e chat no mesmo viewport |
| 18 | Responsividade | Regras 560/740/800/1179/1180px | Desktop 1440/1280, notebook 1024, tablet 768, mobile 390/320, zoom 200% |

## Perfil e fluxo

**Paciente:** `/?`, `/Consulta/Create`, `/Jornada`, `/ListaEspera`, `/Confirmacoes`, `/MinhaConta`, vídeo via `/Jornada?modo=online`. Teste agendamento completo somente com paciente/profissional fictícios e base isolada.

**Médico:** `/MedicoPainel`, `/Jornada`, `/Salas?aba=online`, `/MinhaConta`. Conferir atalho para `/MedicoPainel`, não `/Agenda` (restrita à equipe).

**Atendente:** `/FuncionarioPainel`, `/Agenda`, `/Atendimento`, `/Solicitacoes`, `/ListaEspera`, `/Confirmacoes`, `/RecepcaoPendencias`.

**Administrador:** `/FuncionarioPainel`, `/Organizacao`, `/Agenda`, `/Relatorios`, `/Integracoes`, `/Configuracoes`, `/Acessos`, `/MinhaConta`. Conferir filtros de hoje/7/30 dias, reteste dos textos e do estado vazio.

Para verificar a atualização do PWA, usar uma janela nova ou recarregar após ativação do novo service worker. Mudanças de preferências de tema podem persistir por usuário: restaurar o estado original ao terminar os testes.
