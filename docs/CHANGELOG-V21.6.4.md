# CallMed V21.6.4 — Indigo Horizon

Atualização visual completa baseada na V21.6.3, mantendo a correção de identidade para canais externos e todas as regras de negócio existentes.

## Nova identidade visual

- Tema principal alterado de verde para **azul/índigo**, alinhado à logo CallMed.
- Paleta principal: índigo, azul e ciano, com variantes próprias para tema claro e escuro.
- Sidebar redesenhada com fundo navy, navegação mais clara e estado ativo em gradiente azul/índigo.
- Topbar com efeito translúcido, melhor hierarquia e busca integrada.
- Cards, KPIs, formulários, tabelas, badges, menus e popovers receberam novo acabamento.
- Estados de foco ganharam destaque acessível em azul.
- Dashboard ganhou cards com profundidade e acentos visuais sem poluir a tela.
- Central de Atendimento recebeu novo tratamento para lista de conversas, mensagens e painel de contexto.
- Área do paciente ganhou hero, atalhos e cards com linguagem visual consistente.
- Assistente flutuante recebeu cabeçalho em gradiente e mensagens diferenciadas.
- Mobile/PWA recebeu navegação inferior translúcida e responsividade revisada.

## Páginas públicas e Identity

- Home pública redesenhada com hero mais forte, tipografia maior e cards modernos.
- Login, cadastro, recuperação de senha, logout e acesso negado utilizam a mesma identidade azul/índigo.
- Tema escuro dessas páginas foi revisado para navy/indigo.
- Tela offline e cores do PWA foram convertidas para a nova paleta.

## PWA / Android

- Novo cache: `callmed-static-v21-6-4-indigo-horizon`.
- `manifest.json` usa `theme_color: #4f46e5` e `background_color: #f5f7ff`.
- TWA: `versionCode 2164` e `versionName 21.6.4`.

## Arquivos principais

- `CallMedCrud/wwwroot/css/design-v2164.css`
- `CallMedCrud/wwwroot/css/site.css`
- `CallMedCrud/wwwroot/css/public.css`
- `CallMedCrud/wwwroot/css/identity.css`
- `CallMedCrud/wwwroot/offline.html`
- `CallMedCrud/wwwroot/manifest.json`
- `CallMedCrud/wwwroot/service-worker.js`
- `CallMedCrud/Views/Shared/_Layout.cshtml`
