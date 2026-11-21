# Frontend e acessibilidade — varredura estática inicial

- Base tem 109 `.cshtml`, cinco CSS, JS de navegação/fluxos, e sprite de ícones.
- `nova-00-design.css` tem 143.830 bytes e 6.277 linhas; as cinco folhas têm aprox. 1.291 regras qualificadas de nível superior.
- Foram localizados 26 seletores idênticos que aparecem em mais de uma folha. Isso é candidato a harmonização, não defeito comprovado.
- Sintaxe CSS verificada por `tinycss2`; nenhum erro de parse de nível superior.
- 50 arquivos `.js`, `.cjs`, `.mjs` validados por `node --check`.

**Pendente:** visualizar todas as telas reais em browser autenticado com dados sintéticos (4 perfis), zoom 200%, teclado, contraste WCAG 2.2 AA, estados de erro, loading, modal, breakpoints, chat de três colunas e fluxo de agendamento. Prévia HTML isolada não substitui teste real.

**Sem modificação visual nesta rodada:** não empilhar correções de CSS sem comprovar o problema específico.
