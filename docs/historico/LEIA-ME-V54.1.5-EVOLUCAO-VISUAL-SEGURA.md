# CallMed V54.1.5 · Evolução Visual Segura (R1)

## O que esta entrega faz

Parte **exatamente do ZIP completo V54.1.5 Lembretes/Links** e preserva todas as views Razor, controladores, serviços, JS, rotas, lógica clínica e CSS originais. Em vez de reescrever a interface, adiciona uma folha CSS ao final da cadeia. A aparência passa a ter acabamento em azul-petróleo/verde-água, cabeçalho escuro, cartões com contraste e sombras discretas e foco mais visível.

O sistema conserva as mesmas classes, a logo original, menus, formulários, dashboard, agenda, central em três colunas, perfis e breakpoints da V54.1.5.

## Arquivos modificados/adicionados

1. **Modificado:** `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml` — um `meta` de identificação e uma linha de `link` para o refinamento, carregado após os cinco CSS originais.
2. **Adicionado:** `src/CallMed.Web/wwwroot/css/nova-50-refino-seguro.css` — todas as alterações visuais.
3. **Adicionado:** este guia.

Nenhum dos cinco arquivos `nova-00` até `nova-40` foi modificado ou apagado. Nenhum `.cs`, `.cshtml` de tela, `.js`, banco, serviço de e-mail, configuração de PWA ou manifest foi alterado, exceto `_DocumentHead` com as duas inclusões descritas.

## Como instalar corretamente

- O **ZIP completo** já contém a base V54.1.5 e o refinamento; use para substituir a árvore do projeto *em um branch de teste*, preservando o mesmo layout `CallMed/src/...`.
- O **patch** só deve ser aplicado sobre a **V54.1.5 exata**, e não sobre V56/V57. Substitua `_DocumentHead.cshtml` e adicione a nova folha CSS.
- Antes do deploy, rode `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release`, e teste as quatro contas no ambiente de homologação.
- O `asp-append-version="true"` atualiza a URL do CSS quando seu conteúdo mudar. A lógica e cache do PWA foram mantidos inalterados.
- No navegador, a verificação da versão é:
  `document.querySelector('meta[name="callmed-ui-version"]')?.content`
  Deve retornar `v54.1.5-evolucao-segura-r1`.
- Confirme no Render que o commit com este código foi realmente implantado. Um ZIP baixado não publica automaticamente.

## Como reverter

- Na `_DocumentHead.cshtml`, remova a linha de `<meta name="callmed-ui-version" ... />` e a linha `<link rel="stylesheet" href="~/css/nova-50-refino-seguro.css" asp-append-version="true" />`.
- Opcionalmente apague o arquivo `nova-50-refino-seguro.css`.
- Pronto: a ordem e o conteúdo das cinco folhas originais da V54.1.5 não foram alterados.

## Validações e limitações

- A nova folha foi analisada por parser CSS sem erros.
- Foram comparados os arquivos com o ZIP V54.1.5; nenhuma alteração de lógica, views ou CSS original.
- O pacote ZIP passou no teste de integridade.
- **Não foi possível renderizar a prévia em um navegador local**: o ambiente bloqueou inclusive a navegação para arquivo local/localhost. Isso não é um teste de layout real das quatro contas.
- **Não houve build .NET**, porque o SDK não está instalado no ambiente desta execução.
- O patch não resolve o OAuth do Gmail. A falha de token HTTP 400 precisa de diagnóstico de autorização separado; a Central e os demais canais permanecem como estão.

## Direção visual adotada

Retém o desenho e UX da V54.1.5 em vez de redesenhar páginas. Com uma camada de baixo risco, adiciona tons azul-petróleo/verde-água, superfícies ligeiramente acinzentadas, hierarquia discreta e cabeçalho escuro. Sem barra lateral nova e sem mexer na disposição das funções.
