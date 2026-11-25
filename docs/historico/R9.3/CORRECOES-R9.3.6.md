# CallMed R9.3.6 — Correção dos diálogos compartilhados

**Base:** projeto completo R9.3.5. **Escopo:** visual e usabilidade do componente global de confirmação, sem alterações no banco de dados, regras clínicas, canais ou permissões.

## Defeito reproduzido

A confirmação **“Limpar mensagens da tela”**, acionada pelo botão de limpar do assistente flutuante, mantinha o corpo da janela escuro mas exibia a faixa de ações com `background: rgb(244, 248, 252)` no tema escuro. O contraste da faixa e dos botões destoava do restante da interface. O mesmo componente (`window.CallMedUI.confirm`) é usado por formulários de confirmação de outras áreas.

## Correção

- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`: adicionado conjunto específico de estilos para `dialog.cm-feedback-dialog` no fim do CSS consolidado, com cores coerentes para tema claro/escuro, barra de ações, foco visível, botões de 44px e dimensões responsivas; centralização explícita da janela.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`: novo marcador de versão `r9.3.6-feedback-theme`.
- `src/CallMed.Web/wwwroot/service-worker.js`: nova chave de cache `r936-feedback-theme` para evitar manter CSS anterior no PWA.
- Contratos de cache existentes atualizados para nova versão.
- `tests/check-r936-feedback-browser.py`: novo teste com o código real de `feedback.js` e estilos computados no Chromium.

## Segurança e comportamento

O método de confirmação no `feedback.js` permanece **inalterado**. `Esc`, o X e “Manter mensagens” cancelam a operação; “Limpar mensagens” exige clique explícito. A limpeza se limita à tela do navegador e **não exclui o histórico salvo no sistema**, como descreve o aviso original.

## Validação feita

- Chromium local com tema claro/escuro e viewports **320, 390, 820, 1024 e 1440 px** (sete cenários, incluindo duplicações por tema).
- Confirmação centralizada, sem vazamento lateral, e foco restaurado após Esc.
- Contraste calculado em CSS: descrição escura 8,82:1; botão cancelar escuro 8,83:1; confirmar escuro 5,53:1. Todos acima de 4,5:1.
- Contratos existentes de arquitetura, CSS, MVC, compatibilidade de versões e UI R9.3.1–R9.3.5.
- Preservação das funcionalidades da IA e rotas de perfis; nenhum envio real ou consulta real alterada.

**Ainda pendente:** deploy no Render e validação autenticada dessa versão atualizada. Não foi possível compilar .NET neste ambiente. As capturas locais são representações do componente, não capturas da versão publicada.
