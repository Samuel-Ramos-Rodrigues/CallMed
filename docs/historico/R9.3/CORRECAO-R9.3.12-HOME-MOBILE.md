# CallMed R9.3.12 — Contraste da Home pública no celular

## Evidência e diagnóstico

O print enviado em 23/11/2026 mostrava o `public-hero` da home pública em uma tela móvel com fundo quase branco, mas o título “Sua saúde,” e os textos secundários praticamente invisíveis.

O problema foi **reproduzido** no CSS da R9.3.11. O seletor `body.r5-public .public-hero` fixa `background:#f0f5f0!important`, mas o título e os parágrafos herdam `--text` e `--muted` da raiz `data-theme="dark"` (`rgb(238,243,255)` para o título e `rgb(190,208,213)` para o parágrafo). A cor do botão principal também era uma superfície azul-clara (`rgb(133,161,255)`) com texto branco.

## Correção

- Criado `src/CallMed.Web/wwwroot/css/callmed-r9312-public-home.css`, carregado **por último** pelo `_DocumentHead.cshtml`.
- Título, subtítulo, chamada menor, link secundário e nota de atendimento do hero usam cores escuras próprias do **fundo claro do hero**, sem depender do tema global.
- Link principal usa fundo azul mais escuro e texto branco. O controle tem altura mínima de 48px e ocupa a largura disponível no celular.
- O cartão gráfico escuro (`care-ticket`) mantém seu tema original.
- Versão visual e cache do PWA atualizados para evitar CSS antigo persistido pelo service worker.
- Testes de integridade de CSS e do cabeçalho atualizados para validar também a nova folha de estilo.

## Testes realmente executados

Com Python Playwright + Chromium local, o teste `tests/check-r9312-public-home.py` usa o conteúdo HTML da `Views/Home/Index.cshtml` e as folhas CSS reais da aplicação. Foram testados **20 cenários**: 320, 390, 820, 1024 e 1440 pixels; claro/escuro; alto contraste ligado/desligado.

| Critério | Resultado |
|---|---|
| Contraste dos textos e botões inspecionados | Mínimo calculado de 6,25:1 nos 20 cenários |
| Rolagem horizontal da página no hero | Não encontrada |
| Botão CTA com altura mínima de 44px | Aprovado |
| Botão dentro do limite do card | Aprovado |
| Integridade CSS e contratos de interface | Aprovados |
| Sintaxe do Service Worker | Aprovada |

Screenshots de validação: `artifacts/r9312-home-dark-390.png`, `artifacts/r9312-home-light-390.png`, `artifacts/r9312-home-dark-1440.png`, `artifacts/r9312-home-light-1440.png`.

## Limitações

- A página pública foi representada em Chromium local com a **marcação do hero extraída da Razor** e com os estilos de produção. **Não** foi executado o servidor .NET nesta sessão.
- Não foram executados cadastro, login, operações com pacientes, testes de APIs ou qualquer alteração de dados.
- A nova versão **não foi publicada**; após o deploy, conferir a homepage no Android com a preferência de tema escuro salva, com atualização do Service Worker, e repetir a verificação em páginas autenticadas se houver novos prints.
