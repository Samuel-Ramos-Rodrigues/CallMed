# Validação CallMed R9.3.4

## Executado neste pacote
- `python scripts/check-architecture.py` — 0 problemas.
- `python scripts/check-css.py` — CSS válido.
- `python scripts/check-flows-v54-1-4.py` — 21 contratos passaram.
- `python scripts/check-ui-v54-1-2.py` — 12 contratos passaram.
- `node tests/ux-contract.cjs` — aprovado.
- `node tests/css-integrity.cjs`, `node tests/check-r931-correcoes.cjs`, `node tests/check-r932-visual-contract.cjs`, `node tests/check-r933-ui-contract.cjs` — aprovados.
- `python tests/check-r934-browser.py` — Chromium local: 1440px escuro e claro; 820px escuro; 390px escuro.

### Resultados visuais locais
- Formulário do agendamento fica abaixo do guia, com 18px de separação na montagem de teste.
- Cards inferiores do paciente têm recuo interno de 30px desktop, 20px tablet e 18px celular.
- Assistente no canto: 380×505px desktop, 380×505px tablet, 370×490px mobile (390px).
- A largura do conteúdo não muda ao abrir o chat. O backdrop não aparece e o `aria-modal` não é acionado.
- Fechamento pelo X e pela tecla Escape conferido.
- O modo escuro da etapa destacada evita texto branco sobre fundo claro.

## Verificação pendente no site após deploy
1. Conferir `/Jornada?modo=online`, menu superior e botão contextual "Todas as consultas".
2. Conferir `/Consulta/Create`: o guia não deve cobrir o título "Como deseja ser atendido?".
3. Abrir a IA pela bolha no canto direito, sem overlay escuro nem redimensionamento da página.
4. Verificar `/` no tema escuro: recuo dos quadros inferiores e contraste da etapa ativa.
5. Percorrer outras telas dos quatro perfis em temas claro/escuro, com 390, 820 e 1440px.

**Limite:** O Chromium local avaliou montagens de componentes; a versão R9.3.4 não foi publicada nem testada autenticada em produção nesta rodada. A compilação .NET também não foi executada porque o SDK não está disponível neste contêiner.
