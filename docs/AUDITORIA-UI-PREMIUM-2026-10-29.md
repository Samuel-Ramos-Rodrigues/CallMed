# Auditoria visual CallMed — 29/10/2026

## Problemas confirmados na estrutura

- Os layouts autenticado, público e de autenticação carregavam várias camadas `clinic-*` simultaneamente (`clinic-theme`, `clinic-redesign`, `clinic-final-fixes`, `clinic-polish-2026`, `clinic-unified-palette`, `clinic-functional-fixes`, `visual-system-2026`). A cascata com regras repetidas facilitava regressões e tornava difícil saber qual regra prevalecia.
- O dashboard mostra cartões de resumo com fundo claro e texto quase branco, o que reduz drasticamente o contraste.
- O `theme.js` usa `data-theme="light|dark"`, portanto a identidade deve responder a esse atributo e não depender apenas da preferência do sistema.
- A cor do `meta theme-color` ainda refletia a identidade anterior.

## Alterações feitas

- Criado `wwwroot/css/callmed-premium.css` como sistema visual principal, com paleta índigo/lavanda/ardósia e sem verde como cor de marca.
- Definidas superfícies e textos separados para modo claro e escuro; o modo claro usa fundo azul-acinzentado, não branco puro como fundo geral. O modo escuro usa carvão/azul-marinho e lavanda.
- Centralizados estilos de navegação, cartões, tabelas, formulários, estados de botão (normal, hover, foco, desabilitado), modais, popovers, autenticação e páginas públicas.
- Corrigido explicitamente o contraste de cartões de indicadores/resumo que podem ter fundo claro.
- Removidas dos três layouts principais as camadas `clinic-*` sobrepostas; mantidos os CSS base `site.css`, `modules.css`, `identity.css` e `public.css`, conforme a área, seguidos por `callmed-premium.css`.
- Atualizados `theme-color` e a cor definida pelo JavaScript para a nova paleta.
- Atualizada a identificação do cache do service worker quando a constante de cache correspondente foi encontrada.

## Limitações de validação

Esta foi uma revisão estática do código e dos estilos existentes. O ambiente não dispõe do SDK .NET para confirmar build/runtime, e não foi executado teste visual automatizado em navegador. Views com estilos inline, componentes externos e CSS carregado por páginas específicas ainda podem exigir ajustes por tela. Testar painel, agenda, atendimento, formulários, login/cadastro, relatórios, exames e visualização em dispositivos móveis antes de publicar.
