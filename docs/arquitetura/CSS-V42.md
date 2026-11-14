# CSS V42 — inventário pré-exclusão e contrato de migração

**Origem:** `wwwroot/css/callmed.css` na `master` da V40, commit `8339623`. Inventário realizado antes de apagar o monolito.

## Descoberta
- A `master` continha **1 arquivo CSS**, `callmed.css` (245629 caracteres, 9205 linhas). Não havia CSS antigos adicionais para excluir fisicamente.
- Nele havia **80 nomes de variáveis CSS**, **254 declarações** (incluindo substituições por tema e escopo) e **1135 referências CSS** `var(--...)`.
- Styles históricos V34 a V40 e classes `v16-*`, `cm19-*`, `cm25-*`, `cm37-*` apareciam no monolito; algumas ainda são usadas por views.
- O PWA citava `/css/callmed-auth-v34.css`, não presente na árvore de arquivos; essa referência obsoleta foi eliminada.

## Nova ordem de carregamento (obrigatória)

1. `css/modulos/00-fundamentos.css` — regras preexistentes preservadas na mesma posição relativa
2. `css/modulos/10-componentes.css` — regras preexistentes preservadas na mesma posição relativa
3. `css/modulos/20-portal.css` — regras preexistentes preservadas na mesma posição relativa
4. `css/modulos/30-agenda-atendimento.css` — regras preexistentes preservadas na mesma posição relativa
5. `css/modulos/40-navegacao.css` — regras preexistentes preservadas na mesma posição relativa
6. `css/modulos/50-identidade-tokens.css` — regras preexistentes preservadas na mesma posição relativa
7. `css/modulos/51-identidade-publico.css` — regras preexistentes preservadas na mesma posição relativa
8. `css/modulos/60-jornada.css` — regras preexistentes preservadas na mesma posição relativa
9. `css/modulos/61-operacao.css` — regras preexistentes preservadas na mesma posição relativa
10. `css/modulos/70-paineis.css` — regras preexistentes preservadas na mesma posição relativa
11. `css/modulos/80-tema-v39.css` — regras preexistentes preservadas na mesma posição relativa
12. `css/modulos/90-modulos-internos.css` — regras preexistentes preservadas na mesma posição relativa
13. `css/modulos/95-jornada-unificada.css` — novos estilos da jornada unificada

Os **12 primeiros módulos concatenados nessa ordem** reconstituem byte a byte o `callmed.css` original. O 13º acrescenta o componente unificado da V42. A exclusão física de `callmed.css` e a atualização de `_DocumentHead.cshtml` e do Service Worker devem ocorrer na mesma alteração, para evitar deploy com CSS ausente.

## Variáveis CSS preservadas

| Variável | Declarações | Leituras via var() | Linhas no monolito original |
|---|---:|---:|---|
| `--accent` | 10 | 94 | 20, 51, 4504, 4519, 6361, 8115, 8408, 8416, 8425, … |
| `--accent-soft` | 9 | 67 | 23, 54, 4505, 4520, 8116, 8409, 8417, 8425, 8430 |
| `--bg` | 10 | 15 | 13, 44, 4497, 4512, 6356, 8106, 8406, 8414, 8423, … |
| `--cm-accent` | 2 | 2 | 6272, 6323 |
| `--cm-accent-soft` | 2 | 1 | 6273, 6324 |
| `--cm-action` | 1 | 1 | 6301 |
| `--cm-action-hover` | 1 | 1 | 6302 |
| `--cm-action-ink` | 1 | 4 | 6303 |
| `--cm-bg` | 2 | 4 | 6256, 6343 |
| `--cm-bg-soft` | 2 | 0 | 6257, 6347 |
| `--cm-border` | 2 | 24 | 6266, 6350 |
| `--cm-border-strong` | 2 | 5 | 6309, 6351 |
| `--cm-content` | 1 | 0 | 6291 |
| `--cm-danger` | 2 | 2 | 6280, 6331 |
| `--cm-danger-bg` | 2 | 1 | 6281, 6332 |
| `--cm-font` | 1 | 0 | 6290 |
| `--cm-info` | 2 | 0 | 6282, 6333 |
| `--cm-info-bg` | 2 | 0 | 6283, 6334 |
| `--cm-ink` | 2 | 8 | 6262, 6316 |
| `--cm-ink-2` | 2 | 9 | 6263, 6317 |
| `--cm-motion` | 1 | 3 | 6308 |
| `--cm-muted` | 2 | 27 | 6264, 6348 |
| `--cm-muted-2` | 2 | 2 | 6265, 6349 |
| `--cm-panel-fill` | 1 | 3 | 6305 |
| `--cm-panel-shadow` | 2 | 2 | 6304, 6342 |
| `--cm-primary` | 2 | 26 | 6267, 6318 |
| `--cm-primary-active` | 2 | 0 | 6269, 6320 |
| `--cm-primary-hover` | 2 | 1 | 6268, 6319 |
| `--cm-primary-ink` | 2 | 0 | 6271, 6322 |
| `--cm-primary-soft` | 2 | 7 | 6270, 6321 |
| `--cm-radius-lg` | 1 | 1 | 6288 |
| `--cm-radius-md` | 1 | 1 | 6307 |
| `--cm-radius-sm` | 1 | 0 | 6287 |
| `--cm-radius-xl` | 1 | 0 | 6289 |
| `--cm-refino-linha` | 2 | 1 | 6299, 6340 |
| `--cm-refino-sombra` | 2 | 0 | 6300, 6341 |
| `--cm-ring` | 2 | 0 | 6286, 6338 |
| `--cm-shadow` | 2 | 2 | 6284, 6336 |
| `--cm-shadow-lg` | 2 | 1 | 6285, 6337 |
| `--cm-shadow-sm` | 2 | 4 | 6306, 6335 |
| `--cm-success` | 2 | 2 | 6276, 6327 |
| `--cm-success-bg` | 2 | 0 | 6277, 6328 |
| `--cm-surface` | 2 | 15 | 6258, 6344 |
| `--cm-surface-muted` | 2 | 6 | 6260, 6346 |
| `--cm-surface-raised` | 2 | 0 | 6259, 6345 |
| `--cm-surface-strong` | 2 | 2 | 6261, 6315 |
| `--cm-violet` | 2 | 0 | 6274, 6325 |
| `--cm-violet-soft` | 2 | 0 | 6275, 6326 |
| `--cm-vv-height` | 1 | 0 | 6292 |
| `--cm-warning` | 2 | 0 | 6278, 6329 |
| `--cm-warning-bg` | 2 | 0 | 6279, 6330 |
| `--cm37-bg` | 3 | 5 | 8212, 8228, 8477 |
| `--cm37-ink` | 3 | 10 | 8213, 8229, 8477 |
| `--cm37-line` | 3 | 8 | 8215, 8231, 8478 |
| `--cm37-muted` | 3 | 15 | 8214, 8230, 8478 |
| `--cm37-primary` | 4 | 10 | 8217, 8233, 8238, 8479 |
| `--cm37-primary-ink` | 3 | 1 | 8218, 8234, 8239 |
| `--cm37-soft` | 3 | 4 | 8216, 8232, 8479 |
| `--content` | 2 | 3 | 33, 6063 |
| `--danger` | 4 | 11 | 27, 58, 4507, 4522 |
| `--danger-soft` | 2 | 3 | 28, 59 |
| `--day-count` | 1 | 2 | 6293 |
| `--focus` | 11 | 7 | 32, 61, 4506, 4521, 6062, 6068, 8117, 8409, 8417, … |
| `--header` | 2 | 2 | 34, 3861 |
| `--line` | 10 | 204 | 19, 50, 4503, 4518, 6360, 8112, 8407, 8415, 8424, … |
| `--muted` | 10 | 155 | 18, 49, 4502, 4517, 6359, 8111, 8407, 8415, 8424, … |
| `--navy` | 2 | 0 | 6294, 6339 |
| `--primary` | 9 | 58 | 21, 52, 6060, 6066, 8113, 8408, 8416, 8424, 8429 |
| `--primary-hover` | 9 | 4 | 22, 53, 6061, 6067, 8114, 8408, 8416, 8425, 8430 |
| `--radius` | 1 | 7 | 30 |
| `--radius-sm` | 1 | 1 | 31 |
| `--shadow` | 4 | 14 | 29, 60, 8410, 8418 |
| `--space` | 1 | 0 | 35 |
| `--success` | 4 | 7 | 25, 56, 4508, 4523 |
| `--surface` | 10 | 106 | 14, 45, 4498, 4513, 6357, 8107, 8406, 8414, 8423, … |
| `--surface-2` | 9 | 43 | 15, 46, 4499, 4514, 8108, 8406, 8414, 8423, 8428 |
| `--surface-3` | 9 | 8 | 16, 47, 4500, 4515, 8109, 8406, 8414, 8423, 8428 |
| `--teal` | 4 | 22 | 24, 55, 8409, 8417 |
| `--text` | 10 | 75 | 17, 48, 4501, 4516, 6358, 8110, 8407, 8415, 8424, … |
| `--warning` | 4 | 6 | 26, 57, 4509, 4524 |

As linhas se referem ao arquivo antigo registrado no histórico Git. Uma variável com zero leitura nesta tabela **não é necessariamente órfã**: JS, Razor ou estilos futuros podem usá-la. Duplicidade entre temas não é automaticamente erro. A limpeza de seletores deve depender de cruzamento com views/scripts e testes visuais — não de nomes como `v16` ou `v34`.

## Limites e segurança
- A estrutura `@scope (body.identity-body)` foi mantida integralmente, inclusive partes historicamente agrupadas como público, para preservar cascata e especificidade.
- Entrada e cadastro da V34, Central de Atendimento em 3 colunas, temas e acessibilidade dependem dessas regras e continuam cobertos pelo CI.
- A correção do calendário da **PR #9 (V41)** está fora da `master` de partida; integrar ou reconciliar a PR separadamente antes de publicar a V42.
- Backend, PostgreSQL, autenticação, Gmail, Brevo, Evolution/WhatsApp, httpSMS, jobs e automações **não foram removidos**.

## Próximos passos após aceite visual
Auditar seletores por uso real nas 109 views e scripts; eliminar gradualmente regras comprovadamente órfãs; introduzir tokens semânticos novos somente com testes por perfil, tema e dispositivo.
