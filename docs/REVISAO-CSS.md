# CallMed — revisão CSS atual

**Atualizado em:** 29/10/2026.

A arquitetura de estilos anterior foi substituída por uma folha unificada. O histórico das revisões anteriores foi superado por [`REDESIGN-VISUAL-2026-10-29.md`](REDESIGN-VISUAL-2026-10-29.md).

## Fonte de estilos atual

- `CallMedCrud/wwwroot/css/callmed.css` — tokens de cor, tema claro/escuro, navegação, botões, formulários, tabelas, painéis, agenda, atendimento, autenticação, páginas públicas, contingência, offline, responsividade e impressão.
- `CallMedCrud/Views/Shared/_DocumentHead.cshtml` — carrega a folha unificada nos layouts.
- `CallMedCrud/wwwroot/js/theme.js` e `preferences.js` — preferências de tema.
- `CallMedCrud/wwwroot/service-worker.js` — pré-carrega a folha única e invalida o cache anterior.

As antigas folhas `site.css`, `modules.css`, `public.css`, `identity.css`, `contingencia.css`, `comprovante.css` e as camadas `clinic-*.css` foram removidas do diretório de produção. Não devem ser recriadas em paralelo como correções rápidas; novas regras precisam ser integradas ao sistema visual único e agrupadas por componente.

## Verificação estática desta entrega

- Referências de CSS encontradas em layouts, views e páginas HTML estáticas: resolvidas para o arquivo que existe.
- Referências executáveis a folhas CSS antigas: nenhuma encontrada.
- Parsing do CSS: sem erros reportados.
- JSON do manifesto: válido.
- Conteúdo não CSS da base preservado.

**Ainda não é homologação visual completa.** A compilação .NET e a execução real no navegador não foram feitas neste ambiente. A versão precisa ser compilada e testada localmente antes de ser publicada, inclusive estados de hover, foco, desabilitado, tema escuro e larguras móveis.
