# CallMed — auditoria de estilização V51

**Base:** pacote completo CallMed V50 que inclui APK Release 50.0.0 assinado pelo proprietário. A V51 é uma auditoria da camada Web e dos contratos visuais; o Android nativo e suas APIs foram mantidos.

## Diagnóstico do print enviado

1. **Assistente virtual:** o FAB e o painel `mksan-ai-widget` do Razor existiam, mas não possuíam layout completo em nenhum dos cinco estilos carregados. O resultado eram ícones, mensagens e comandos soltos no rodapé. **Corrigido** com popover de mensagens, FAB e estados de aberto/fechado (`.open`) correspondentes ao JavaScript existente.
2. **Textos diminutos:** havia regras locais para 9–11 px. **Corrigido** com tipografia-base 15 px no desktop / 14 px no celular, tabelas legíveis, rótulos e botões de 12–14 px, sem transformações de escala global.
3. **Largura do conteúdo:** a camada V50 já permitia maior largura; a V51 adota 1480 px de largura máxima e margens responsivas. A captura publicada parecia ter **zoom do navegador reduzido**; confirmar com `Ctrl+0` no navegador, pois CSS não pode modificar o zoom do Brave.
4. **Compatibilidade de tema escuro:** superfícies coloridas continham tokens de texto incompatíveis, produzindo texto claro em cartões claros. **Corrigido** contraste em destaques, botões e métricas.
5. **Página secundária sem CSS:** Android, notificações, avaliações, gestão e conta tinham componentes não contemplados pelos cinco CSS. **Cobertos** com regras de layout específicas e estilos estruturais genéricos.
6. **Cadastro de convênio:** a área `campos-convenio` deve ser escondida enquanto o JavaScript não marca `.ativo`; foi corrigida.
7. **Responsividade:** breakpoint da recepção era sobreposto por regras de especificidade maior; agenda no celular era estreita. **Corrigido** em telas menores que 800 px.
8. **Cache PWA:** nome de cache estático atualizado de `callmed-static-v100-modo` para `callmed-static-v101-coverage`, junto aos testes que conferem os cinco CSS.

## Inventário por funcionalidade

O levantamento percorreu 109 arquivos Razor, 51 controllers e 64 templates com formulário, abrangendo 39 diretórios/grupos de view (incluídas importações). **Cada entrada abaixo é um módulo encontrado no código**, e não uma funcionalidade inventada.

| Grupo Razor | Funcionalidade identificada no código | Views | Tratamento visual |
|---|---|---:|---|
| `Acessos` | Gestão de permissões e usuários | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Agenda` | Agenda clínica e filtros por período | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Aplicativo` | Página pública de instalação/download Android | 1 | Regras específicas de estilo V51 + base unificada |
| `Atendimento` | Central multicanal, criação de conversas, contexto e revisão IA | 3 | Regras específicas de estilo V51 + base unificada |
| `Auditoria` | Eventos de auditoria e busca | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Avaliacoes` | Formulários de avaliação e resultados | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Checkin` | Chegada por QR e terminal de recepção | 2 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Configuracoes` | Configurações operacionais | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Confirmacoes` | Confirmação e resposta ativa de consultas | 1 | Regras específicas de estilo V51 + base unificada |
| `Consulta` | Consulta, detalhes, equipes, remarcação e formulário de agendamento | 8 | Regras específicas de estilo V51 + base unificada |
| `Convenios` | Convênios e regras de atendimento | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Disponibilidade` | Gestão de vagas, horários e bloqueios | 6 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Disponibilidades` | Disponibilidades de equipe e ajustes | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Especialidade` | Cadastro de especialidades | 4 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Funcionario` | Cadastro de funcionários e alterações | 6 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `FuncionarioPainel` | Início da recepção e ações do dia | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `HistoricoExames` | Triagem, pendências e histórico de exames | 2 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Home` | Início do paciente, privacidade, termos e erros | 4 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Identity` | Login, cadastro, recuperação de senha e acesso negado | 8 | Regras específicas de estilo V51 + base unificada |
| `Integracoes` | Canais WhatsApp, e-mail e SMS, conectividade e status | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Jornada` | Preparação, ações, teleconsulta e continuidade do atendimento | 5 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `ListaEspera` | Vagas e ofertas da lista de espera | 2 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Medico` | Cadastro de médicos e agendamento semanal | 6 | Regras específicas de estilo V51 + base unificada |
| `MedicoAcesso` | Gestão de acesso de profissionais | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `MedicoPainel` | Próximo atendimento e linha do tempo clínica | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `MinhaConta` | Dados, convênio, foto, configurações e conta | 2 | Regras específicas de estilo V51 + base unificada |
| `Operacao` | Indicadores e decisões de operação | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Organizacao` | Diretório dos módulos e menus | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Paciente` | Cadastro e manutenção dos pacientes | 6 | Regras específicas de estilo V51 + base unificada |
| `Piloto` | Métricas e acompanhamento da demanda SENAI | 2 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Privacidade` | Gestão de privacidade e solicitações | 2 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `Psicologia` | Acervo clínico e PEP | 2 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `RecepcaoPendencias` | Pendências humanas e exceções do check-in | 1 | Regras específicas de estilo V51 + base unificada |
| `Relatorios` | Indicadores, gráficos e exportação | 1 | Regras específicas de estilo V51 + base unificada |
| `Salas` | Teleconsulta, sala e gestão da presença | 1 | Regras específicas de estilo V51 + base unificada |
| `Shared` | Layout base, cabeçalhos, menus, popovers e assistente virtual | 13 | Regras específicas de estilo V51 + base unificada |
| `Solicitacoes` | Fila, tarefas, solicitações, contingência e detalhes | 6 | Regras específicas de estilo V51 + base unificada |
| `_ViewImports.cshtml` | Importações globais | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |
| `_ViewStart.cshtml` | Layout padrão | 1 | Formulário/ação existente preservado; controles e estados recebem padrões base |

## Contratos técnicos preservados

- Não houve alteração em controllers, services, banco, migrations, rotas, nomes ou IDs de campo, endpoint de API, autenticação ou fluxo de revisão IA.
- Os **cinco arquivos CSS** originais seguem sendo os únicos carregados por `_DocumentHead.cshtml`, sem necessidade de inserir um CSS extra.
- Android/`CallMed.apk` continua em **50.0.0**, com `versionCode` 5000, bytes e SHA-256 idênticos aos metadados `release.json`. A chave `.jks` privada não foi adicionada.
- Modificados: `nova-40-modulos.css`, `nova-20-fluxos.css`, `nova-30-acesso.css`, `service-worker.js`, `tests/css-integrity.cjs`, `scripts/check-css.py`. Adicionados apenas relatórios e capturas sintéticas desta revisão.
- Foram adicionados estilos que respondem às classes e IDs existentes nas views; não foi criada uma segunda implementação das funcionalidades.

## Validação executada

| Checagem | Resultado |
|---|---|
| CSS com `tinycss2` (5 folhas) | 0 erros de análise sintática |
| `node tests/css-integrity.cjs` | Aprovado |
| `node tests/ux-contract.cjs` | Aprovado |
| `python scripts/check-css.py` | Aprovado |
| `python scripts/check-architecture.py` | 0 problemas |
| `python scripts/auditar-views-callmed.py` | 109 Razor, 51 controllers, 393 referências MVC, 57 arquivos estáticos; 0 referências inválidas |
| `python scripts/auditar-ux-paginas.py` | 0 críticos, 0 de revisão; 16 apontamentos informativos |
| `node --check` | 26 scripts Web válidos |
| Chromium/Playwright | 60 combinações (seis views sintéticas × cinco larguras × tema claro/escuro): 0 rolagens horizontais e 0 erros JS |
| UI de assistente e notificações | Abertura/fechamento simulados (sem backend) |
| APK V50 integrado | SHA e tamanho coerentes com `release.json` |

## Limites da verificação

- **Não foi possível executar `dotnet build`**, porque o SDK .NET não está instalado neste ambiente.
- **Não foi executado login ou fluxo de pacientes em produção**, nem realizada auditoria visual em cada uma das 109 views autenticadas. As 60 medições do Chromium usam seis páginas representativas com fixtures sintéticas e CSS real.
- As informações e indicadores das prévias são fictícios. Elas não substituem testes completos de recepção, médico, administrador, paciente, teleconsulta e canais.
- O **GitHub e o Render ainda não foram atualizados** com esta correção. Para visualizar no site publicado é necessário integrar o conteúdo do ZIP ao repositório e implantar, preferencialmente em ambiente de homologação.

## Checklist para implantação / homologação

1. Fazer backup do código e das configurações do ambiente publicado.
2. Subir as alterações de CSS e service worker sem tocar na chave de assinatura.
3. Executar `dotnet build CallMed.sln -c Release` e os testes da solução.
4. No navegador, usar `Ctrl + 0` para restaurar zoom a 100%, atualizar com `Ctrl + Shift + R` após deploy e verificar o novo service worker.
5. Testar os quatro perfis com dados de homologação; focar em Assistente IA, Central, agenda, filtros, cadastro, confirmações, salas e download do APK.
6. Verificar o aplicativo Android em aparelho físico e conferir se o download Web fornece o Release V50.

**Prioridade:** corrigir legibilidade e assistente antes de novas mudanças estéticas; validar funcionalidades reais antes de promover em produção.
