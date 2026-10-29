# CallMed — paleta consistente, atalho de atendimento e redesign

## Alterações desta entrega

- Restaurado o atalho **Atendimento — Central de conversas** no bloco **Acesso rápido** do painel de funcionário/administrador, apontando para a rota existente `Atendimento/Index`.
- Reforçada a paleta clínica no último CSS carregado para que as variáveis de tema sejam aplicadas de forma consistente em fundo, cartões, filtros, campos, conversas e mensagens.
- Ajustados o cabeçalho da Central de Atendimento, os cartões de indicadores, filtros, lista de conversas, cabeçalho do chat, balões e área de composição.
- O redesign preserva rotas, formulários, ações de assumir/devolver/encerrar conversa, filtros e dados reais do sistema.
- Mantidos os estilos específicos de alto contraste; o tema claro/escuro continua baseado nas variáveis de tema.
- Atualizado o cache do service worker para `callmed-static-v39-dashboard-attendance-palette-refresh`.

## Validação executada

- Parser CSS: sem erros de sintaxe no arquivo `clinic-functional-fixes.css`.
- Confirmada a existência do atalho e a rota `Atendimento/Index` na view do painel.
- Confirmado o incremento do nome do cache do service worker.
- A compilação .NET não foi executada neste ambiente porque o SDK `dotnet` não está instalado. A publicação deve ser homologada no ambiente de build/Render e conferida visualmente em desktop e celular.
