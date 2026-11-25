# CallMed R9.3.3 — Rodapé, contraste de textos e assistente virtual

**Base:** CallMed R9.3.2 entregue anteriormente. **Entrega:** projeto completo independente, sem merge no GitHub nem deploy no Render.

## Origem dos problemas

A revisão ao vivo de `/Consulta/Create`, autenticada como paciente na R9.3.2, confirmou que o assistente aberto sobrepunha a área do formulário. A inspeção de código encontrou a regra R9.1 `body.cm-r8-shell.cm9-shell .app-footer { background:#fff!important; color:#54677a!important; }`, sem correção específica para o tema escuro. O CSS acumulava regras legadas de R5/R7/R8/R9.1 que em certos cards, tabelas e links também prevaleciam sobre as cores do tema escuro.

## Alterações

1. **Rodapé:** cores do tema escuro aplicadas de forma explícita sobre o seletor legado. Fundo azul-escuro `#14293c`, texto legível, links em verde-água claro e realce de foco/hover. Tema claro preservado.
2. **Textos e superfícies:** contraste de placeholders e textos secundários; fundo e títulos de cartões antigos R8/R9.1; listas de atividades, consultas, prioridades, estados vazios, tabelas e controles. Ajustes em telas de agenda, painel médico/atendente/administrador, jornada e Central de Atendimento, com escopo limitado ao tema escuro.
3. **IA:** removido o redimensionamento de 410px da `app-main` ao abrir o chat. A janela passa a ser **diálogo centralizado** em desktop e tablet, com fundo escurecido, foco contido, fechamento por Escape, X ou clique fora; no celular permanece em tela inteira. O formulário não é comprimido e não fica parcialmente utilizável por trás de um painel lateral. Ajustados balões, feedback e textos do chat para ambos os temas.
4. **Cache:** incrementados a marcação de versão no `_DocumentHead.cshtml` e `CACHE_NAME` do service worker para permitir atualização dos assets CSS/JS.
5. **Testes:** contratos estáticos e teste real de CSS computado/interações em Chromium local usando um documento isolado representativo das páginas. O teste cobre 1440px, 820px e 390px, modos claro/escuro, cores do rodapé e cards, abertura/fechamento e ausência de redimensionamento pelo chat.

## Arquivos alterados

- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`
- `src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js`
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`
- `src/CallMed.Web/wwwroot/service-worker.js`
- `scripts/check-v35-classic-visual-contracts.py`
- `tests/css-integrity.cjs`
- `tests/check-r931-correcoes.cjs`
- `tests/check-r932-visual-contract.cjs`
- `tests/check-r933-ui-contract.cjs` (novo)
- `tests/check-r933-browser.py` (novo)
- `README.md` e estes documentos (orientações)

## Validação e limites

Os testes visuais automatizados foram executados **em Chromium local, com HTML de teste que usa o CSS e JS reais**, não sobre a aplicação publicada R9.3.3. O navegador em produção foi inspecionado na **R9.3.2** antes da correção. Não houve compilação do .NET neste ambiente e não houve teste de todas as páginas autenticadas com a nova versão no Render. A homologação final depende de publicar uma versão de teste e revisitar as páginas listadas no roteiro.

Nenhum banco de dados, integração, credencial, modelo clínico ou fluxo de negócio foi modificado.
