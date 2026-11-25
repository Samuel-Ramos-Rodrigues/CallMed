# CallMed R9.3.10 — Ajustes após as capturas da Central e menu de funções

**Origem:** R9.3.9, projeto independente. **Escopo:** layout da Central de Atendimento e redundância da busca no modal de funções. Não foram alteradas permissões, banco de dados, integrações, conteúdo das mensagens, perfis ou ações de envio.

## Defeitos demonstrados

1. **Central pequena e exigindo rolagem da página:** a área de atendimento tinha altura `calc(100dvh - 198px)` ou `min-height:calc(100dvh - 190px)` mesmo após um cabeçalho relativamente alto. Isso frequentemente levava a janela inteira a rolar. Além disso, a largura podia ser limitada por regras herdadas do container principal.
2. **Três colunas desbalanceadas:** a terceira reservava altura artificial na ficha do paciente, criando grande vazio antes das ações rápidas. A R9.3.9 já reduziu parte disso, mas faltava adequar o tamanho da central ao viewport.
3. **Resumo da lista apagado:** contadores de conversas na base eram mostrados com contraste insuficiente e fonte de cerca de 10px.
4. **Modal “Mais funções”:** a seção já oferecia um campo pesquisável; o botão “Buscar uma função” levava a outro diretório e duplicava o objetivo do campo.

## Mudanças

- CSS **escopado** por `body.central-context`: `.app-main` ocupa a largura disponível, e `.atendimento-shell` mantém 3 colunas em desktop (até 1101px). Em larguras menores, preserva as abas existentes.
- `js/atendimento/central-fit.js` observa tamanho do cabeçalho/viewport e ajusta só a altura do shell. A lista de conversas e as mensagens rolam internamente; a página evita rolagem vertical extra causada pelo histórico. Em telas muito baixas, permite rolagem externa para não cortar controles.
- Contadores no rodapé da caixa têm fundo e contraste próprios para claro/escuro e tipografia de 11–12px.
- O modal conserva o campo de busca e suas categorias; foi removido apenas o atalho de busca redundante. A rota `/Organizacao` continua acessível normalmente pelos seus caminhos de navegação existentes.
- Versão e cache PWA atualizados para distinguir R9.3.10.

## Cobertura efetivamente testada

| Teste | Resultado |
|---|---|
| Chromium **local**, estrutura representativa com CSS real, 390/820/1024/1101/1440/1920 × 900px | Passou nos seis cenários |
| Em 1101/1440/1920px, shell ocupa pelo menos 90% da largura | Passou |
| Em desktop, extremidade inferior do shell dentro do viewport | Passou |
| Rolagem interna da lista e das mensagens, com 16 conversas e 20 mensagens simuladas | Passou |
| Ações rápidas logo abaixo da ficha, sem espaçamento artificial | Passou |
| Contadores legíveis no tema escuro | Verificação de cor calculada aprovada |
| JavaScript da altura | `node --check` aprovado |
| Scripts de arquitetura, CSS e quatro contratos JavaScript/UX | Aprovados após atualizar expectativas para a segunda folha de estilos usada desde R9.3.9 |
| O botão redundante foi removido sem remover o campo de pesquisa | Conferido no Razor |
| `dotnet build`, Razor renderizado com dados reais e navegação autenticada pós-deploy | **Não executados** neste ambiente |

> **Limite:** o HTML do teste Chromium é um fixture local representativo das classes do projeto. Não é a renderização real do Razor autenticado com banco e não representa homologação do CallMed em produção.

## Após implantar em homologação

Verifique em `Atendimento/Index` nos perfis Atendente e Admin a 390, 820, 1024 e 1440px: (1) conversas e mensagens rolam internamente; (2) composer continua visível; (3) painel contextual permanece acessível; (4) abrir/fechar filtros não corta ações; (5) rodapé e janela não colidem; (6) `Mais funções` mantém busca funcional, sem botão repetido. Recarregue com `Ctrl+Shift+R` caso o PWA tenha CSS antigo.
