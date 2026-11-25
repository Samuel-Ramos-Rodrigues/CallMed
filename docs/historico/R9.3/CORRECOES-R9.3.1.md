# CallMed R9.3.1 — Correções pontuais de interface e indicadores

**Base:** ZIP independente CallMed R9.3 organizado fornecido pelo responsável.  
**Escopo:** alterações de código e estilos, sem executar migração de banco, modificar credenciais ou publicar em produção.

## Ajustes aplicados

1. **Tema escuro.** Corrigidos os cartões da página `/Organizacao` que herdavam fundo branco e texto claro das regras R5/R9.1. Ajustados os itens ativos do menu, barras contextuais, indicadores administrativos e centro do gráfico. Os overrides ficam **no final do CSS R9.3 existente**, sem criar outro estilo principal. Tema claro preservado.
2. **Indicadores administrativos.** O painel `/FuncionarioPainel` passa a abrir com **últimos 30 dias**, apresentando o número real de consultas do período. O usuário pode selecionar Hoje, Próximos 7 dias, Próximos 30 dias ou Últimos 30 dias. Confirmadas e remarcadas são agrupadas de acordo com a tela de consultas existente. Canceladas são excluídas da distribuição; os demais status aparecem em “Outros estados”. Sem dados, o gráfico mostra estado vazio e não `0%` sem denominador. O cabeçalho informa datas, e o KPI de vagas recuperadas identifica que seu período é o **mês atual**. Os números do painel do atendente continuam restritos ao dia, com consultas não canceladas.
3. **Navegação por perfil.** O paciente não vê mais atalhos de Agenda/Recepção em Confirmações. A Jornada oferece “Minha agenda” ao médico, em vez de `/Agenda` (restrita à equipe). O diretório `/Organizacao` apresenta atalhos auxiliares de Integrações somente ao administrador, Indicadores somente à equipe e destinos próprios ao médico/paciente. A listagem de funções já era filtrada por `NavegacaoCallMed.Itens(User)` e foi preservada.
4. **Data do médico.** O título do painel médico passou a usar `ClinicaClock.Hoje`, o mesmo relógio usado pelo controller para selecionar as consultas, em vez de `DateTime.Today` do servidor.
5. **Assistente flutuante.** Contraste, posição e visibilidade do botão “Fechar” foram reforçados no cabeçalho do chat, incluindo layout móvel e tema escuro. A lógica de envio/IA não foi alterada.
6. **Notificações.** O título acessível do sino reflete o número real, inclusive zero; o painel atualizado também sincroniza metadados e rótulo. O último contador para avisos do dispositivo é armazenado por usuário, na sessão do navegador, evitando reaproveitar estado entre contas.
7. **PWA.** Cache estático renomeado para `callmed-static-v5415-clinic-r931-fixes` para evitar uso de CSS anterior à correção. A marcação de versão no head foi atualizada.

## Arquivos principais

- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`
- `src/CallMed.Web/Controllers/Paineis/FuncionarioPainelController.cs`
- `src/CallMed.Application/PresentationModels/Paineis/FuncionarioPainelViewModel.cs`
- `src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml`
- `src/CallMed.Web/Views/Organizacao/Index.cshtml`
- `src/CallMed.Web/Views/Confirmacoes/Index.cshtml`
- `src/CallMed.Web/Views/Jornada/{Index,Consulta}.cshtml`
- `src/CallMed.Web/Views/MedicoPainel/Index.cshtml`
- `src/CallMed.Web/Views/Shared/Components/Confirmacoes/Default.cshtml`
- `src/CallMed.Web/wwwroot/js/atendimento/confirmacoes.js`
- `src/CallMed.Web/wwwroot/service-worker.js`

## Verificações locais realizadas

**Passaram:**
- `python scripts/check-architecture.py`
- `python scripts/check-css.py`
- `python scripts/check-v35-classic-visual-contracts.py`
- `python scripts/auditar-views-callmed.py` (referências MVC e estáticos)
- `python scripts/check-ui-v54-1-2.py`
- `python scripts/check-flows-v54-1-4.py` (expectativa atualizada ao texto real da recepção)
- `python scripts/check-v52-visual-contracts.py`
- `python scripts/validar-demanda-senai.py` (referências legadas atualizadas à estrutura R9.3)
- `node tests/css-integrity.cjs`
- `node tests/check-r931-correcoes.cjs`
- `node --check` nos scripts JS alterados e no service worker

**Limites:** SDK .NET 10 e navegador autenticado com aplicação local **não estavam disponíveis no ambiente de edição**. Não foi possível rodar build .NET, migrations ou testes end-to-end autenticados sobre o ZIP corrigido. Os testes anteriores do site publicado não validam este ZIP até um novo deploy.

## Teste após publicar (banco de homologação e contas fictícias)

- **Admin, tema escuro**: `/Organizacao` — cabeçalho, seções e cards legíveis; item ativo do topo não fica branco. Testar tema claro também.
- **Admin**: `/FuncionarioPainel` — checar os quatro períodos do seletor; confirmar total = soma das três categorias; no período vazio, mostrar mensagem. Conferir que vagas recuperadas têm rótulo mensal.
- **Médico**: `/Jornada` e `/Jornada/Consulta/{id}` (consultas fictícias) — “Minha agenda” abre o painel médico sem 403. Data do painel alinhada ao horário da clínica.
- **Paciente**: `/Confirmacoes` — não aparecem links para recepção ou agenda geral. `/Organizacao` apresenta funções próprias.
- **Atendente**: `/Atendimento`, `/Solicitacoes`, `/ListaEspera`, `/Salas` — comparar temas, áreas de navegação e rolagem.
- **Todos**: abrir e fechar assistente no desktop e no celular; alternar tema e abrir o sino; conferir que o contador e a lista de pendências correspondem.
- **PWA**: atualizar serviço, limpar cache legado se necessário, recarregar em modo online e validar fallback `offline.html`/`contingencia.html`.

Não testar envio de mensagens, agendamento ou videoconferências com dados clínicos reais sem autorização específica.
