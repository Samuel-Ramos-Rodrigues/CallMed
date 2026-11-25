# CallMed R9.3.9 — Auditoria profunda e correções visuais

**Base:** R9.3.8, projeto completo e independente. **Alvo:** corrigir defeitos demonstrados nas capturas de 23/11/2026 dos perfis Paciente, Médico, Atendente e componentes globais. **Segurança:** nenhuma alteração em banco, autenticação, regras de agendamento ou integrações externas.

## 1. Evidência → causa → correção

| Tela/Perfil | Evidência observada | Causa no projeto | Alteração R9.3.9 |
|---|---|---|---|
| Paciente, início, modo simplificado | Atalhos claros com textos ilegíveis | Sobreposição de cores antigas | Correção R9.3.8 preservada; reteste claro/escuro/alto contraste |
| Paciente, menu de conta | **Sair da conta** claro e apagado | Botão de logout com estilos herdados | Contraste explícito para o botão de logout, claro e escuro |
| Login | Metade direita branca e formulário escuro; rodapé desconectado | Regra `body.r5-auth .identity-panel` força `#f5f7f5` | Painel, cartão, campos e rodapé em uma paleta azul consistente |
| Médico, agenda clínica | **Horário/Data/Estado** com fundo quase branco e texto claro | `cm9-clinical-facts` força `#f4f8fa` | Fundo e textos coerentes com tema, separadores e números legíveis |
| Médico, agenda clínica | Links **Todas as consultas / Salas online** duplicados | Toolbar repetia links do cabeçalho | Toolbar mantém apenas período Hoje, data e contagem; navegação permanece no cabeçalho |
| Médico/equipe, Jornada | Faixa horizontal vazia entre breadcrumb e título | `r5-module-brief` escondia título mas preservava links redundantes | Remoção da faixa na Jornada; comandos próprios da página mantidos |
| Atendente, Agenda | Faixa de atalhos duplicados e calendário difícil de ler | Barra extra e eventos pequenos em grade com rolagem | Barra redundante removida; eventos usam fonte mínima maior e rolagem interna |
| Atendente, Central | Faixa redundante e terceira coluna com grande vazio antes das ações | `cm-context-card` herdava `min-height:100%` | Barra redundante removida; painel de contexto cresce conforme conteúdo e ações vêm logo abaixo |
| Atendente, Recepção/Pendências | Quatro itens brancos com letras quase invisíveis | `r3-exception-item` herda superfície clara sem texto correspondente | Cor de fundo e textos ajustados por tema, botões com largura mínima |
| Médico/Equipe, salas online | `07:0050 min` e **Detalhes da consulta** quebrado em quatro linhas | Grid colocava ações em primeira coluna estreita; horário/duração inline | Horário e duração em linhas separadas; área exclusiva para ações; CSS responsivo |
| Todos, **Todas as funções** | Cards/accordions brancos com títulos brancos e botão X apagado | `cm-modulo-grupo` herdava superfícies e texto de versões diferentes | Background, títulos, links e fechamento por tema, inclusive alto contraste |
| Global, IA e confirmação | Modal e rodapé com cores incompatíveis | CSS compartilhado legado | Correções R9.3.6/R9.3.7 preservadas, folha nova mantém o estilo da IA |

**Nota sobre o print da Agenda extremamente pequena:** a captura indica um nível de zoom do navegador possivelmente muito reduzido (também miniaturiza o cabeçalho). O CSS não controla o zoom configurado pelo navegador. O teste de tamanho deve ser repetido com `Ctrl+0` (100%), além da verificação responsiva. A R9.3.9 melhora fontes dos eventos e rolagem, mas não modifica o zoom do usuário.

## 2. Arquivos alterados ou adicionados

- `src/CallMed.Web/wwwroot/css/callmed-r939-audit-fixes.css` (novo, ajustes **escopados**, separado do CSS histórico)
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml` (carregamento do novo CSS e versão)
- `src/CallMed.Web/wwwroot/service-worker.js` (cache do PWA atualizado)
- `src/CallMed.Web/Views/Salas/Index.cshtml` (horário/duração estruturados)
- `src/CallMed.Web/Views/MedicoPainel/Index.cshtml` (redução de navegação duplicada, plural correto)
- `src/CallMed.Web/Views/Jornada/Index.cshtml` (remoção de faixa redundante)
- `src/CallMed.Web/Views/Agenda/Index.cshtml` (remoção de faixa redundante)
- `src/CallMed.Web/Views/Atendimento/Index.cshtml` (remoção de faixa redundante)
- `tests/css-integrity.cjs`, `tests/check-r931-correcoes.cjs`, `tests/check-r932-visual-contract.cjs`, `tests/check-r933-ui-contract.cjs`, `tests/check-r935-permissions.cjs` (contratos de versão/cobertura atualizados)
- `tests/browser/check-r939-visual.py` (teste de CSS calculado e layout em Chromium)

Não foram modificados controllers, services, banco, API Evolution, Brevo/Gmail, httpSMS, WebRTC, segurança ou cadastro de usuários.

## 3. Testes efetivamente executados

| Teste | Resultado |
|---|---|
| Chromium local: componentes com classes reais do Razor em 1440, 1024, 820, 390 px | 8 cenários aprovados |
| Tema escuro e claro | Aprovado nos cenários acima |
| Alto contraste nos componentes alterados | 2 cenários aprovados |
| Login coerente com tema, fundo e rodapé | 2 cenários aprovados |
| Contraste dos textos medidos (fundos/textos das capturas) | Mínimo 4,5:1 nos pares testados |
| Salas: botão de detalhes com pelo menos 130 px e dentro do viewport | Aprovado nos 8 cenários |
| Salas: horário/duração visualmente separados | Aprovado nos 8 cenários |
| Viewport sem scroll horizontal da página no fixture | Aprovado nos 8 cenários |
| Central: ações contextuais logo abaixo da ficha, sem min-height artificial | Aprovado no cenário desktop local |
| Simulação adicional de acessibilidade (modo simplificado + alto contraste) | 6 combinações de tema/largura conferidas |
| JavaScript `node --check` | 27 arquivos, zero erros |
| Testes estáticos de arquitetura, CSS, navegação e permissões | 6 scripts aprovados |
| `dotnet build` e integração .NET com banco | **Não executados**: SDK .NET ausente no ambiente |
| Navegação autenticada nas páginas da R9.3.9 em Render | **Não executada**: versão ainda não publicada |
| Testes de envio WhatsApp/SMS/e-mail e videochamadas reais | **Não executados** por segurança/escopo visual |

> **Atenção:** testes Chromium utilizam um fixture HTML com a **estrutura e classes reais** dos componentes extraídas das views, mas NÃO executam a renderização Razor/servidor ASP.NET nem reproduzem o banco. Não confundir com testes de ponta a ponta autenticados.

## 4. Como testar após o deploy

1. Faça deploy da R9.3.9 em **homologação** primeiro e confirme no HTML a meta `callmed-ui-version` com `r9.3.9`.
2. Reinicie o navegador ou use `Ctrl+Shift+R` para evitar cache desatualizado do PWA.
3. Paciente: confirme modo simplificado (atalhos), alto contraste (incluindo ambos juntos), logout, agendamento e abertura do chat no canto.
4. Médico: confirme `MedicoPainel` (quadro horário/data/estado), `Jornada` (sem faixa de links duplicados) e `Salas?aba=online` (hora/duração e botão legíveis).
5. Atendente: confira `RecepcaoPendencias`, `Agenda` em zoom 100%, `Atendimento` em 3 colunas e diretório `Mais funções`.
6. Admin: `FuncionarioPainel`, `Organizacao`, `Auditoria`, `Integracoes` e `MinhaConta` em ambos os temas.
7. Repita as telas em 390, 820, 1024 e 1440 px, temas claro/escuro, alto contraste e teclado (Tab, Shift+Tab, Esc), e faça os fluxos reais em um banco de testes isolado.
8. Caso haja diferença, anote a **URL exata**, perfil, tema, zoom e versão da meta; isso permite identificar se o deploy/caching trouxe os arquivos novos.

## 5. Limitações e próximos passos

- O projeto ainda contém uma folha CSS histórica de aproximadamente 523 KB e várias regras `!important`. A solução evita apagar regras de negócio por engano e separa as novas correções em uma folha específica; **não representa uma remoção total de CSS legado**.
- As telas e componentes sem prints ainda requerem inspeção visual autenticada após o deploy. Não afirmar que todo o CallMed foi homologado.
- Os dados e permissões reais não foram alterados; qualquer erro de autorização/fluxo deve ser validado no ambiente de teste com contas específicas.
