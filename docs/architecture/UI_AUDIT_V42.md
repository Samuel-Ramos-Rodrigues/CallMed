# CallMed V42 — Auditoria transversal das páginas

Data: 14/11/2026. Escopo: branch `feature/v42-jornada-css-modular` da PR #11.
Este documento registra observações confirmadas no código e nos testes automatizados; não afirma que as contas reais ou o deploy de produção foram inspecionados.

## Cobertura

- 109 arquivos Razor `.cshtml` inventariados, incluindo 24 layouts/parciais. Não são 109 endpoints independentes.
- 131 declarações de formulários, 18 tabelas e 20 imagens `img` contabilizadas.
- 48 cenários de URL na suíte `tests/browser/check-design.cjs`, com perfis simulados (paciente, profissional, atendente, administrador e visitante); temas claro/escuro, verificações axe, geometria e rolagem. Outras suítes verificam navegação, identidade, calendário, central, teleconsulta, PWA, Android e fluxos.
- `scripts/auditar-views-callmed.py` valida referências a controllers/actions, recursos estáticos e navegação móvel.
- `scripts/auditar-ux-paginas.py` inventaria marcação estática em **todas** as views e publica `artifacts/ui-page-audit.json` como artifact do CI.

## Resultado-base da auditoria transversal

Na primeira execução da nova verificação (GitHub Actions run 43635267374), foram encontrados **19 itens a revisar, nenhum crítico**:
- 12 ocorrências de identificadores no código-fonte Razor: ligadas a ramificações `if/else/switch` ou seleções alternativas, não equivalentes a 12 IDs duplicados no DOM final. Exigem verificação por rota/estado renderizado antes de corrigir.
- 4 estilos inline: 1 cor fixa de cadastro (removida), 1 propriedade `--day-count` dinâmica da agenda e 2 barras de percentual calculadas pelo servidor no relatório.
- 2 buscas sem `aria-label` explícito mas **aninhadas a elementos `label`**, portanto não presumir ausência de nome acessível.
- 1 `onclick` do botão Imprimir comprovante (substituído por ação declarativa e handler delegado).

O scanner foi refinado para distinguir campos de busca já contidos em rótulos e IDs alternativos em fluxo Razor. O diagnóstico não deve ser confundido com auditoria clínica ou de permissão em produção.

## Páginas e responsabilidades

| Superfície | Áreas/Views prioritárias | Contrato UX e riscos |
|---|---|---|
| Pública e identidade | Identity Login, Register, recuperações, Home público | Preservar login aprovado, logo, validação e legibilidade em mobile |
| Paciente | Home, Consulta, Jornada, MinhaConta, Checkin, Salas | Agendamento guiado em etapas, acompanhamento da consulta, feedback de ações e acesso claro à teleconsulta |
| Profissional | MedicoPainel, Medico, Jornada/Tele, Psicologia | Indicadores legíveis, próximos pacientes, ações autorizadas e acompanhamento da consulta |
| Atendente | FuncionarioPainel, Agenda, Atendimento, Solicitacoes, ListaEspera, RecepcaoPendencias | Priorizar tarefas, fila, confirmação e encaixe; preservar Central em três colunas |
| Administrador | Integracoes, Configuracoes, Operacao, Relatorios, Acessos, Auditoria | Separar status técnico, indicadores, gestão de usuários e ações de risco |
| Componentes comuns | _Layout, _AuthLayout, _DocumentHead, _NavegacaoPrincipal, _MenuModulos | Responsividade, buscas, foco, tema, feedback e assets versionados |

## Correções de baixo risco aplicadas

1. `Register.cshtml`: estilo de apresentação inline substituído pela classe existente `identity-showcase-note`; nenhuma variável de tema apagada.
2. `Consulta/Details.cshtml` + `js/core/shell.js`: impressão comandada por `data-cm-print`, sem `onclick` inline; protegido por `tests/ux-contract.cjs`.
3. `Relatorios/Index.cshtml`: rótulo geral corrigido de “Psicólogos ativos” para “Profissionais ativos”, abrangendo todas as especialidades.
4. CI: relatório completo das views e seu JSON adicionados, sem exigir dados de pacientes.

## Dependências mantidas

- Os 13 módulos CSS iniciais reproduzem byte a byte a cascata V41; `99-consultation-journey.css` acrescenta apenas a jornada. A verificação SHA impede exclusões de estilos que quebrariam telas invisíveis ao teste.
- Manter assinatura dos tokens usados por CSS/JS; não excluir seletor apenas por parecer repetido.
- A navegação por perfil é regida pelo backend, não por CSS ou por links da página.
- Não modificar canais (Gmail API/Brevo, Evolution, SMS/httpsms), IA, check-in ou motor de vagas nesta tarefa de UX.

## Dívidas técnicas para a próxima rodada

1. `Consulta/_Form.cshtml` mistura markup Razor e script de calendário/agendamento extenso. Extrair para JS externo **somente** com contrato de dados explícito, testes da edição/criação e regressão de mês/horário.
2. Barras de relatório usam largura dinâmica via `style`; uma substituição por `<progress>` acessível exige revisar a aparência e os valores com dados reais.
3. Deduplicar tokens e seletores dos 13 módulos apenas em pequenos lotes, preservando teste de visualização antes/depois de cada lote. Os módulos são a separação física segura, ainda não uma simplificação semântica.
4. Completar revisão manual das capturas Playwright por perfil, em 320/390/768/1280px, alto contraste, 200% de zoom e telas com listas vazias/longas. A suíte automatizada reduz regressões, mas não substitui revisão editorial.
5. Conferir estados dinâmicos e autorizações reais no ambiente de homologação (contas de teste); não usar pacientes reais para capturas ou logs.

## Critério para merge

Compilação, testes de banco, browser, qualidade, CSS integrity, scanner de views, artifact `ui-page-audit.json` e revisão visual dos quatro perfis. Nenhum merge/deploy automático antes de rever essas evidências.
