# CallMed — correção de compatibilidade visual (30/10/2026)

## Base usada
- Projeto de origem: `CallMed-auditoria-css-estrutura-corrigida-2026-10-29.zip`
- CSS histórico de referência: `CallMed-cadastro-termos-senha-forte(1).zip`
- O CSS antigo foi consultado para recuperar o contrato de tokens e as classes reais das telas. As cores verdes antigas NÃO foram reintroduzidas.

## Identidade visual preservada
Mantidos os tokens `--cm-*` índigo/lavanda da versão atual. Foram recriados aliases para todos os nomes de variáveis encontrados no conjunto CSS histórico:
`--bg`, `--surface`, `--surface-alt`, `--ink`, `--muted`, `--line`, `--brand`, `--brand-soft`, `--navy`, `--accent`, estados semânticos, `--clinic-*` e `--mk-*`.
Todos os aliases apontam para a paleta atual; nenhum token histórico restaura a paleta verde.

## Problemas comprovados nas capturas
1. `Consulta/_Form.cshtml` gera `.cm19-doctor-option`, `.cm19-date-option` e `.cm19-time-option` por JavaScript. O CSS anterior estilava nomes diferentes (`.cm19-choice-card`, `.cm19-date-choice`, `.cm19-time-choice`), fazendo os botões renderizarem com aparência padrão do navegador.
2. `.cm19-booking-section-head > span` tinha largura fixa de 36 px, insuficiente para `Etapa 1`, causando quebra do texto. O cabeçalho agora usa grid com áreas separadas para etapa, título e descrição.
3. `.cm21-a11y-options` herdava largura fixa de 290 px, apropriada para o popover do cabeçalho, mas incorreta para a página Minha Conta. Agora o painel da página usa largura total e grid responsivo; o popover mantém largura compacta.
4. O botão `.cm19-smart-slot` foi ajustado para manter ícone e texto alinhados em linha, com título e descrição empilhados.

## Arquivos alterados nesta correção
- `CallMedCrud/wwwroot/css/callmed.css`
- `CallMedCrud/wwwroot/service-worker.js` (cache atualizado para `callmed-static-v44-booking-a11y-fix`)
- Este relatório.

## Verificações
- CSS parseado com tinycss2: 0 erros de parsing.
- Referências `var(--...)` sem definição: 0.
- Todos os nomes de variáveis encontrados no CSS histórico têm definição/alias na folha atual.
- Classes CSS alinhadas com os nomes gerados pelo JavaScript do formulário de consulta.
- Não foi possível executar o Chromium nem compilar .NET neste ambiente; portanto, esta entrega não é apresentada como homologação visual/runtime completa.
