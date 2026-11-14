# Inventário e migração do CSS — CallMed V42

Origem auditada: `src/CallMed.Web/wwwroot/css/callmed.css`, blob Git `8b99b4dbe4b21ae5c6722efd07a9b47a5baee768` (base V41).
Arquivo original: 252956 caracteres e 9459 linhas. A árvore contém **um único CSS legado**, não vários arquivos para apagar.
**Regra desta migração:** concatenação dos módulos reproduz integralmente o CSS original, na ordem original. Não fizemos remoção arbitrária de declarações nem mudanças de seletor.

## Ordem da cascata
1. `src/CallMed.Web/wwwroot/css/modules/00-foundations.css` — 16026 caracteres
2. `src/CallMed.Web/wwwroot/css/modules/10-navigation.css` — 13877 caracteres
3. `src/CallMed.Web/wwwroot/css/modules/20-attendance.css` — 14408 caracteres
4. `src/CallMed.Web/wwwroot/css/modules/30-scheduling.css` — 53326 caracteres
5. `src/CallMed.Web/wwwroot/css/modules/40-public-and-components.css` — 31829 caracteres
6. `src/CallMed.Web/wwwroot/css/modules/50-senai-workflows.css` — 15442 caracteres
7. `src/CallMed.Web/wwwroot/css/modules/55-identity-public.css` — 35823 caracteres
8. `src/CallMed.Web/wwwroot/css/modules/60-doctor-ui.css` — 4924 caracteres
9. `src/CallMed.Web/wwwroot/css/modules/65-dashboards.css` — 12009 caracteres
10. `src/CallMed.Web/wwwroot/css/modules/70-clinical-theme.css` — 10834 caracteres
11. `src/CallMed.Web/wwwroot/css/modules/80-patient-and-operations.css` — 20310 caracteres
12. `src/CallMed.Web/wwwroot/css/modules/90-internal-modules.css` — 16823 caracteres
13. `src/CallMed.Web/wwwroot/css/modules/95-booking-calendar.css` — 7325 caracteres

## Variáveis e onde são usadas
Variáveis declaradas: 80. Valores duplicados podem ser sobreposições por tema/escopo; **não são apagados automaticamente**.
| Variável | Definições | Referências var() | Exemplos de valores |
|---|---:|---:|---|
| `--accent` | 10 | 94 | #a797ff · #5733c5 · #d9cbff |
| `--accent-soft` | 9 | 70 | #29244b · #efeaff · #21143e |
| `--bg` | 10 | 15 | #0b1120 · #f4f7fc · #000 |
| `--cm-accent` | 2 | 2 | #096F91 · #69D3FF |
| `--cm-accent-soft` | 2 | 1 | #E5F3FA · #14364B |
| `--cm-action` | 1 | 1 | linear-gradient(135deg, #7041EB, #4324B4) |
| `--cm-action-hover` | 1 | 1 | linear-gradient(135deg, #6234DA, #38209A) |
| `--cm-action-ink` | 1 | 4 | #FFFFFF |
| `--cm-bg` | 2 | 4 | #F4F6FB · #090D19 |
| `--cm-bg-soft` | 2 | 0 | #EDF1F8 · #0E1423 |
| `--cm-border` | 2 | 24 | #DDE4EF · #2A334B |
| `--cm-border-strong` | 2 | 5 | #8391AA · #7484A5 |
| `--cm-content` | 1 | 0 | 1640px |
| `--cm-danger` | 2 | 2 | #b42342 · #ff9cad |
| `--cm-danger-bg` | 2 | 1 | #ffeaee · #482638 |
| `--cm-font` | 1 | 0 | Manrope, "Segoe UI", system-ui, sans-serif |
| `--cm-info` | 2 | 0 | #155b94 · #9dceff |
| `--cm-info-bg` | 2 | 0 | #e4f1ff · #1d3553 |
| `--cm-ink` | 2 | 8 | #19243D · #F5F5FF |
| `--cm-ink-2` | 2 | 9 | #35435E · #E2E6FA |
| `--cm-motion` | 1 | 3 | 150ms ease |
| `--cm-muted` | 2 | 27 | #52627E · #B7C1D9 |
| `--cm-muted-2` | 2 | 2 | #60708B · #9CAAC5 |
| `--cm-panel-fill` | 1 | 3 | var(--cm-surface) |
| `--cm-panel-shadow` | 2 | 2 | 0 8px 28px #19214208 · 0 12px 38px #00000024 |
| `--cm-primary` | 2 | 26 | #5C52CD · #B69AFF |
| `--cm-primary-active` | 2 | 0 | #3E359B · #CFCBFF |
| `--cm-primary-hover` | 2 | 1 | #4A40B5 · #D0BAFF |
| `--cm-primary-ink` | 2 | 0 | #FFFFFF · #19142E |
| `--cm-primary-soft` | 2 | 7 | #EEECFF · #292248 |
| `--cm-radius-lg` | 1 | 1 | 18px |
| `--cm-radius-md` | 1 | 1 | 14px |
| `--cm-radius-sm` | 1 | 0 | 10px |
| `--cm-radius-xl` | 1 | 0 | 22px |
| `--cm-refino-linha` | 2 | 1 | var(--cm-border) · #354461 |
| `--cm-refino-sombra` | 2 | 0 | var(--cm-shadow-sm) · 0 8px 25px rgba(0,0,0,.15) |
| `--cm-ring` | 2 | 0 | 0 0 0 3px #5C52CD26 · 0 0 0 3px #AA79FF35 |
| `--cm-shadow` | 2 | 2 | 0 8px 26px #1725440A · 0 10px 32px #0000001F |
| `--cm-shadow-lg` | 2 | 1 | 0 24px 72px #0B10203D · 0 22px 60px rgba(0, 0, 0, .32) |
| `--cm-shadow-sm` | 2 | 4 | 0 2px 8px #17254405 · 0 2px 8px #0000000A |
| `--cm-success` | 2 | 2 | #16745f · #8be0c1 |
| `--cm-success-bg` | 2 | 0 | #e2f5ed · #163b37 |
| `--cm-surface` | 2 | 15 | #FFFFFF · #121827 |
| `--cm-surface-muted` | 2 | 6 | #F6F8FC · #181F32 |
| `--cm-surface-raised` | 2 | 0 | #FFFFFF · #191F32 |
| `--cm-surface-strong` | 2 | 2 | #E8EDF7 · #222B43 |
| `--cm-violet` | 2 | 0 | #7658d8 · #b7a4ff |
| `--cm-violet-soft` | 2 | 0 | #f0eaff · #30294c |
| `--cm-vv-height` | 1 | 0 | 100dvh |
| `--cm-warning` | 2 | 0 | #8a5a08 · #ffcf79 |
| `--cm-warning-bg` | 2 | 0 | #fff1d4 · #49351d |
| `--cm37-bg` | 3 | 5 | var(--surface) · #fff |
| `--cm37-ink` | 3 | 10 | var(--text) · #16202a |
| `--cm37-line` | 3 | 8 | var(--line) · #c9d3db |
| `--cm37-muted` | 3 | 15 | var(--muted) · #4a5763 |
| `--cm37-primary` | 4 | 10 | var(--primary) · #0b5e4f · #8ce3b8 |
| `--cm37-primary-ink` | 3 | 1 | #fff · #10251e |
| `--cm37-soft` | 3 | 4 | var(--surface-2) · #e8f4f1 |
| `--content` | 2 | 3 | 1440px · 1510px |
| `--danger` | 4 | 11 | #ffb4bf · #af2745 · #ffb7c7 |
| `--danger-soft` | 2 | 3 | #3b2030 · #fff0f3 |
| `--day-count` | 1 | 2 | 7 |
| `--focus` | 11 | 8 | #c5b9ff · #6036d0 · #fff |
| `--header` | 2 | 2 | 78px · 68px |
| `--line` | 10 | 209 | #2a3952 · #d5deec · #aaa |
| `--muted` | 10 | 160 | #acbad1 · #52637b · #eee |
| `--navy` | 2 | 0 | #29325f · #202849 |
| `--primary` | 9 | 64 | #6540db · #6036d0 · #5f66e9 |
| `--primary-hover` | 9 | 4 | #7651e9 · #4e26ba · #7077f3 |
| `--radius` | 1 | 7 | 18px |
| `--radius-sm` | 1 | 1 | 11px |
| `--shadow` | 4 | 15 | 0 14px 42px #02071330 · 0 10px 34px #273d5e0b · 0 12px 32px rgba(21,62,45,.045) |
| `--space` | 1 | 0 | 24px |
| `--success` | 4 | 7 | #88e4c1 · #167254 · #a3ffd6 |
| `--surface` | 10 | 109 | #111b2e · #fff · #080808 |
| `--surface-2` | 9 | 44 | #17243a · #f1f4fa · #151515 |
| `--surface-3` | 9 | 8 | #202f48 · #e7edf7 · #252525 |
| `--teal` | 4 | 22 | #53ddc2 · #087b69 · #0B6752 |
| `--text` | 10 | 79 | #edf3ff · #16243c · #fff |
| `--warning` | 4 | 6 | #ffda91 · #805700 · #ffe4a3 |

## Variáveis referenciadas sem declaração literal
- Nenhuma identificada

## Dependências que foram atualizadas
- `_DocumentHead.cshtml`: links em ordem dos 13 módulos com `asp-append-version`.
- `service-worker.js`: cache versionado e registro de todos os novos arquivos; referências a `callmed.css` e `callmed-auth-v34.css` removidas.
- `tests/css-integrity.cjs`: valida a concatenação contra o SHA Git original, ordem no head e cache do PWA.
- `.github/workflows/callmed-quality.yml`: teste de integridade antes de compilar.

## Como manter
Não recriar arquivos `vXX-refino.css`. Novos seletores devem ir ao módulo funcional apropriado. Nunca alterar a ordem das folhas sem validar desktop/celular, tema claro/escuro, login V34 e Central 3 colunas.
Esta fase modulariza fisicamente o CSS. Deduplicação de regras e tokens será uma segunda etapa, de risco maior, condicionada a evidência de testes visuais.
Este branch contém também o calendário responsivo da PR nº 9 (V41), da qual deriva. Não fazer dois merges paralelos dessas correções sem conciliar as branches.

## Complemento V42

Adicionado `99-consultation-journey.css`, posterior aos **13 módulos que preservam o CSS base**. Ele contém somente a nova linha do tempo em `Consulta/Details.cshtml`. O teste SHA valida os 13 arquivos de base e verifica que o novo módulo está linkado e presente no PWA. Total: **14 arquivos CSS**.
