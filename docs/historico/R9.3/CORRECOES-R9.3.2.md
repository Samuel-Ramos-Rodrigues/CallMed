# CallMed R9.3.2 — Correções de contraste, agendamento e assistente

**Base:** R9.3.1 corrigida (ZIP independente). **Escopo:** interface web, sem migração, alteração de banco, credenciais, integrações ou deploy. Um único CSS principal permanece: `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`.

## Problema reproduzido na captura do paciente

Em `/Consulta/Create` no tema escuro, o guia **Seu caminho em 5 passos** aparecia quase branco e com texto de baixo contraste. A causa era uma regra antiga (`body.cm-r5-shell .r5-booking-page .cm50-booking-guide`, que forçava `#f1f5fa!important`), sobrepondo tokens do tema. O chat flutuante também cobria o formulário.

## Mudanças no código

1. **Guia de agendamento:** um banner compacto com tokens de tema, sem segunda lista estática redundante. O formulário permanece como única fonte do progresso dos cinco passos.
2. **Etapas dinâmicas:** classes `active` e `done` continuam compatíveis, com o novo `data-state` (`current`, `complete`, `upcoming`), `aria-current="step"`, rótulo de estado e anúncio `aria-live`. O botão Confirmar continua desabilitado até todos os campos obrigatórios serem preenchidos.
3. **Tema claro/escuro:** o CSS R9.3 único ganha overrides finais, de maior especificidade, para os componentes antigos do agendamento, agenda, solicitações, fila, salas e cabeçalhos. Placeholder, selects, cards, botão inativo, calendário e estado sem horários recebem cores legíveis.
4. **Assistente:** desktop com largura >=1180px passa a reservar espaço na área de conteúdo quando aberto; tablet abre como diálogo com backdrop e foco contido; celular preserva tela cheia. `Escape`, botão fechar e clique no backdrop fecham o diálogo. O tema e os balões da conversa usam tokens compatíveis com o modo escuro.
5. **Indicadores:** o estado de período sem consultas agora é explicado dentro do gráfico, com ícone, mensagem e indicação de que aparecerá quando houver dados. Mantida a consulta do controller R9.3.1: filtros `hoje`, `proximos7`, `proximos30`, `ultimos30`; o KPI de vagas recuperadas continua explicitamente mensal.
6. **Navegação/controles:** mantidas as correções R9.3.1 de permissões de paciente/médico, contador do sino, botões e acessibilidade; reforçados indicadores ativos e o placeholder de busca.
7. **Responsividade:** regras específicas até 800px e 560px ajustam guia, progressão, ações e chatbot. Adaptação de tela de 740px para baixo preservada; tablets entre 741px e 1179px usam diálogo, não sobreposição livre.
8. **PWA:** versão do head e nome do cache do service worker atualizados para que o navegador não mantenha arquivos da R9.3.1.

### Arquivos modificados

- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`
- `src/CallMed.Web/Views/Consulta/Create.cshtml`
- `src/CallMed.Web/Views/Consulta/_Form.cshtml`
- `src/CallMed.Web/Views/Shared/_AssistenteFlutuante.cshtml`
- `src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js`
- `src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml`
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`
- `src/CallMed.Web/wwwroot/service-worker.js`
- `tests/css-integrity.cjs`, `tests/check-r931-correcoes.cjs`, `scripts/check-v35-classic-visual-contracts.py`
- `tests/check-r932-visual-contract.cjs` (novo)

## Verificações executadas

**Aprovadas:** arquitetura, CSS principal/offline, integridade CSS, contratos de UI, navegação, fluxos, referências MVC, verificação R9.3.1, verificação R9.3.2, análise sintática do JavaScript e parse completo do CSS por PostCSS. Na revisão de views, foram analisadas 113 views Razor, 522 referências MVC e 55 recursos estáticos, sem referência inválida.

**Contraste dos tokens (cálculo matemático, não inspeção de renderização):**
- Guia escuro: `#F0F9FC` / `#1C3348`: 12,16:1
- Texto auxiliar escuro: `#BED3E1` / `#1C3348`: 8,41:1
- Etapa atual: `#FFFFFF` / `#105C62`: 7,69:1
- Guia claro: `#15344B` / `#F2F8FA`: 12,05:1

**Não executadas:** build e testes de integração .NET (SDK indisponível neste ambiente); testes Playwright com fixture real (dependem de build/infra); auditoria visual com Chromium local (o navegador não concluiu a execução neste ambiente). Portanto, os testes **em tamanhos reais de celular, tablet, notebook e desktop**, com sessão autenticada, **precisam ser feitos após o deploy**.

## Observações de segurança/funcionamento

- **Não houve deploy automático** nem alteração no GitHub por esta entrega.
- Nenhum dado clínico, agendamento ou mensagem real foi modificado.
- Nenhuma senha foi adicionada a código, README ou relatórios.
- A lógica de confirmação, envio, IA/WhatsApp/SMS e políticas de acesso foi preservada.
- O leitor de tela recebe status das etapas, mas a conformidade WCAG geral do sistema não foi certificada.

Leia `docs/TESTES-VISUAIS-R9.3.2.md` para validar as 18 solicitações em cada perfil antes do merge/deploy.
