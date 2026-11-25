# CallMed NEXT V54.1.3 — Ajustes de links com aparência de botões

Base: CallMed NEXT V54.1.2 **já corrigida** (agenda, alertas e UX). Este hotfix é complementar, não substitui o anterior.

## Mudanças

- Uniformizado o comportamento visual de links que parecem botões (`a.btn`, variantes e `a.cm50-btn`), inclusive foco por teclado, clique, largura máxima e responsividade.
- Ajustadas variantes secundárias e destrutivas no estado de hover sem transformar links em ações POST.
- Aumentada a área clicável dos links de ação das tabelas (`a.row-action`), particularmente Consultas.
- Ajustado o offset de âncoras internas para evitar que o cabeçalho fixo esconda o destino.
- O atalho **Gerenciar disponibilidade** do médico agora expande a seção recolhida de horários antes de navegar até ela.
- Os links **Anterior** e **Próxima** da paginação de avaliações passam a declarar `asp-action="Index"` explicitamente, preservando mês, canal e página.
- Nenhum controller, endpoint ou permissão foi modificado; links continuam sendo `<a>` e ações de formulário continuam sendo `<button>`.

## Validação

- Sintaxe JS e CSS e auditoria estática de links MVC.
- Requer build `dotnet build CallMed.sln -c Release` e conferência manual após deploy (390px, 768px e desktop).
- Não há alteração de banco, mensageria ou variáveis de ambiente.
