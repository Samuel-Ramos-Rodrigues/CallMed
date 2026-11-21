# CallMed V54 — paleta verde + navegação suave

## Referência solicitada
`CallMed · Sistema completo(2).html` (arquivo fornecido pelo usuário).

- Branco `#FFFFFF` como fundo principal.
- Texto grafite `#16202A`, secundário `#4A5763`.
- Verde primário `#0B5E4F`; superfícies suaves `#E8F4F1`.
- Linhas `#C9D3DB`, sucesso `#1E6B3A`, aviso `#8A4B00`, erro `#9B1C1C`.
- A versão escura usa tons complementares de verde, preservando escolha explícita do usuário.

## Fluxos e usabilidade
- Navegação atual, três colunas de atendimento, rotas e formulários preservados.
- Botões de navegação e links de módulos recebem estados hover, ativo e foco mais claros.
- Busca de funções `Ctrl+K`: Enter abre o primeiro resultado, setas navegam nos links.
- Transições sutis entre páginas quando o navegador suporta View Transitions; respeitam preferência de movimento reduzido.
- Rolagem suave em âncoras quando permitido.
- Tema claro como escolha inicial para instalações sem preferências salvas; escolhas existentes não são sobrescritas.
- Manifesto PWA, offline/contingência e cor da barra do navegador atualizados para a referência.
- Novo identificador de cache estático para impedir reaproveitamento do CSS antigo pelo PWA.

## Escopo
Somente CSS, `preferences.js`, `theme.js`, `shell.js`, metadado de cor, service worker, teste estático e esta nota foram modificados.
**Não modifica** controllers, banco, migrations, integração de WhatsApp/Gmail/SMS, lógica de IA nem código nativo do Android. O APK V50 foi preservado.

## Como testar
1. `node tests/css-integrity.cjs`
2. `node tests/ux-contract.cjs`
3. `python3 scripts/check-css.py`
4. `python3 scripts/check-architecture.py`
5. Build .NET + testes reais por perfil e atualização da PWA no deploy.

Observação: este pacote não faz merge na master nem deploy automático.
