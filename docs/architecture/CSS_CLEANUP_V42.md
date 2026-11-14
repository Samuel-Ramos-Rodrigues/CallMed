# Limpeza de CSS — CallMed V42

Data: 14/11/2026. Branch: `feature/v42-jornada-css-modular`.

## Método e escopo

A partir dos 13 módulos CSS da V41, identificamos declarações que tinham **o mesmo seletor, propriedade e contexto de @media/@scope**, mas uma declaração posterior substituía completamente o valor anterior na cascata. Foram removidas **43 declarações redundantes** em 6 módulos. A transformação não excluiu seletores por aparência, não alterou os 14 módulos carregados e não removeu CSS de calendário, jornada, atendimento, formulários ou identidade por hipótese de desuso.

Antes de escrever as mudanças, foi comparado o mapa de **6.686 combinações finais** de seletor/contexto/propriedade na cascata. O mapa permaneceu idêntico depois da transformação, com **zero valores finais alterados** nesse modelo estático. Isso fundamenta a equivalência de declarações, mas a confirmação em layout real depende dos testes de navegador.

## Resultados por arquivo

| Arquivo | Declarações sobrescritas removidas |
| --- | ---: |
| `00-foundations.css` | 16 |
| `10-navigation.css` | 6 |
| `20-attendance.css` | 4 |
| `30-scheduling.css` | 10 |
| `40-public-and-components.css` | 5 |
| `50-senai-workflows.css` | 2 |
| **Total** | **43** |

Também foi corrigido o cabeçalho desatualizado que descrevia `00-foundations.css` como única folha do site.

## Integridade e regressão

- Git blob do CSS anterior: `8b99b4dbe4b21ae5c6722efd07a9b47a5baee768`.
- Git blob da concatenação dos 13 módulos limpos: `66e6cc81f3f9be61afd2804096e3295c50307009`.
- `tests/css-integrity.cjs` protege esse estado, bem como fronteiras dos módulos, ordem da cascata e PWA.
- Suítes de navegador com os quatro perfis simulados, temas, acessibilidade, calendário V41 e central de atendimento V42 devem passar antes de integrar.
- O inventário de 80 tokens foi mantido. Alguns tokens de compatibilidade ainda não são referenciados em CSS, mas não foram apagados sem auditar o consumo por JavaScript.

## Preservado por segurança

Temas claro/escuro, login e cadastro, navegação superior e móvel, logo horizontal, Central em três colunas, Agenda e calendário de sete colunas, Teleconsulta, IA, canais, rotas, controllers, banco de dados e permissões. O documento cobre uma limpeza controlada de CSS; **não** equivale a remover todos os estilos históricos ou redesenhar todas as páginas.

## Próxima revisão futura

Verificar seletores candidatos a desuso frente a Razor, JS e páginas geradas (classes dinâmicas); avançar a limpeza em pequenos lotes com capturas de comparação. Também revisar o JavaScript embutido em `Consulta/_Form.cshtml`, sem misturar essa migração com alterações no comportamento dos agendamentos.
