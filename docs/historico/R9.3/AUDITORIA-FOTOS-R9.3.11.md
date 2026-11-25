# CallMed R9.3.11 — Revisão das capturas e reestruturação da Agenda

**Base:** R9.3.10 independente. **Data:** 23/11/2026. **Implantação:** nenhuma alteração no GitHub ou Render.

## O que foi observado nas imagens

| Tela mostrada | Evidência visível | Situação nesta entrega |
|---|---|---|
| **Agenda Geral** — Administrador/Atendente | Grade semanal com dezenas de linhas altas, enormes espaços escuros vazios, textos pálidos em profissionais e slots claros, rolagem vertical longa | **Corrigido nesta versão:** grade profissional×dia substituída por listas independentes **por dia**; horários agrupados por profissional. Área do calendário tem rolagem interna horizontal/vertical e cabeçalho do dia fixo, com contraste definido para cada estado. |
| **Central de Atendimento** — Atendente | Três colunas estreitas, necessidade de rolar a página inteira e contador da lista apagado | **Correção herdada da R9.3.10:** layout ajustado ao viewport e rolagem interna das listas e mensagens; validado novamente em seis larguras locais. |
| **Inteligência da clínica** — Administrador | Dashboard com filtros, indicadores e gráfico | Nenhum defeito inequívoco na captura. Mantido e não modificado. |
| **Mais funções** | Botão redundante “Buscar uma função” e categorias com contraste fraco no escuro | **Herdado R9.3.10/R9.3.9:** botão redundante removido; categorias e links receberam cores temáticas. |
| **Salas online** | “Detalhes da consulta” quebrado letra por letra e horário/duração concatenados | **Herdado R9.3.9:** coluna e ações reorganizadas; contraste de estados preservado. |
| **Painel Médico** | Quadro claro de horário/data/estado ilegível | **Herdado R9.3.9:** fundo e texto corretos por tema. |
| **Jornada do Médico** | Faixa superior quase vazia e atalhos repetidos | **Herdado R9.3.9:** faixas redundantes retiradas. |
| **Pendências da Recepção** | Cartões brancos, nomes e horários quase invisíveis em modo escuro | **Herdado R9.3.9:** cartões com superfícies e texto escuros/claros coerentes. |
| **Login** | Meia página branca, cartão escuro e rodapé desconectado | **Herdado R9.3.9:** superfícies ajustadas para identidade escura. |
| **Modo simplificado/Alto contraste** | Atalhos brancos com texto quase branco e alto contraste sem efeito | **Herdado R9.3.8:** regras de acessibilidade com prioridade adequada; verificadas novamente nos cenários da Agenda. |
| **Menu da conta** | Botão “Sair da conta” com contraste ruim | **Herdado R9.3.9:** cores próprias. |
| **Confirmar limpeza da IA** | Barra inferior clara no modo escuro | **Herdado R9.3.6/R9.3.7:** confirmação global com cores coerentes, sem alterar comportamento. |

**Diferença importante:** “Herdado” significa que a alteração está preservada no código-fonte entregue. Não implica que a versão do site em produção já a utilize, nem que todos os fluxos correspondentes tenham sido homologados ao vivo.

## Causa estrutural da Agenda

A implementação anterior produzia um `grid` com **uma linha por profissional e uma coluna por dia**. Cada linha assumia a altura da célula que contivesse mais horários, deixando buracos nas demais células. O bloco crescia por muitos viewports, produzindo rolagem da página e dificultando comparar os dias.

A R9.3.11 passa a agrupar as entradas por **dia → profissional → horário**, sem cruzar as alturas dos profissionais. Na semana, os dias são colunas independentes. No modo **Dia**, a coluna ocupa a largura disponível. O modo **14 dias** e os filtros foram preservados. Cliques em consultas levam aos detalhes; em vagas livres, à criação de consulta com profissional, dia e horário preselecionados; bloqueios permanecem não interativos. Nenhuma regra de agendamento, banco ou integração foi alterada.

## Arquivos alterados nesta rodada

- `src/CallMed.Web/Views/Agenda/Index.cshtml` — reestruturação exclusiva da apresentação em Dia/Semana.
- `src/CallMed.Web/wwwroot/css/callmed-r939-audit-fixes.css` — estilos escopados do calendário: tema escuro/claro, estados, rolagem e alto contraste.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml` — identificador visual R9.3.11.
- `src/CallMed.Web/wwwroot/service-worker.js` — versão de cache R9.3.11.
- `scripts/check-ui-v54-1-2.py`, `scripts/check-v35-classic-visual-contracts.py` — contratos ajustados à nova disposição e à versão de cache.
- `tests/check-r9311-agenda-browser.py` — cenário Chromium reproduzível de contraste, rolagem e independência das colunas.

## Testes executados

- **16 cenários de Agenda**: 390, 820, 1024 e 1440 px × claro/escuro × normal/alto contraste. Todos passaram para rolagem interna, ausência de overflow horizontal da página, presença dos sete dias e contraste mínimo 4,5:1 dos textos verificados. Contraste alto alcançou 21:1 nos estados avaliados.
- **6 cenários de modo Dia**: 390, 820, 1440 px × claro/escuro. Sem overflow horizontal da página; grid de um dia usa a largura disponível.
- **Regressão de outros componentes**: 12 cenários locais da auditoria R9.3.9 (Médico, Recepção, Salas, modal de funções e autenticação) aprovados.
- **Central de Atendimento**: seis larguras locais (390 a 1920 px), com rolagem de conversas/mensagens e ações acessíveis, aprovadas.
- `scripts/check-architecture.py`, `scripts/check-css.py`, `scripts/auditar-views-callmed.py`, `scripts/check-ui-v54-1-2.py`, `scripts/check-flows-v54-1-4.py` aprovados; auditoria de views retornou 0 referências inválidas.

### Limites dos testes

O Chromium local renderizou **estruturas de teste com as classes e os CSS reais**. Isso não é renderização Razor autenticada nem prova de uma navegação completa no banco. **Não foi possível executar o `dotnet build`** porque o SDK .NET não está disponível neste ambiente. Não foram executadas operações de agendamento, cancelamento, comunicações, consultas clínicas ou videochamadas em produção.

## Verificação após publicação em homologação

1. Entrar como **Administrador e Atendente** e abrir `/Agenda` no modo **Semana**, com várias consultas e horários livres; rolar dentro do calendário e comparar os grupos de profissionais em cada dia.
2. Confirmar que o cabeçalho de cada dia fica visível durante a rolagem interna, que as colunas avançam horizontalmente sem ampliar toda a página e que o menu do sistema continua acessível.
3. Alternar Claro/Escuro e Alto contraste, além de 390/820/1024/1440 px, verificando nomes, estado Bloqueado e legenda.
4. Trocar Dia / Semana / 14 dias, profissional, especialidade e semana, confirmando que os filtros se conservam.
5. Apenas em dados **QA de homologação**, abrir os detalhes de uma consulta e o formulário de uma vaga livre; não efetuar alteração em registros reais durante a auditoria.
6. Repetir os cenários das outras fotos (Médico, Recepção, Central, Salas e funções). Recarregar com `Ctrl+Shift+R` se o navegador mantiver recursos antigos no cache PWA.

**Esta é uma correção da versão entregue no ZIP, não do site que está atualmente publicado.**
