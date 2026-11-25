# CallMed V54.1.5 — Evolution R3

## Objetivo e base

Esta R3 foi feita **sobre a V54.1.5 + Evolução R1/R2**, sem importar os CSS ou layouts da V55–V57. Ao contrário da R2, há uma reformulação efetiva de partes do HTML/Razor com dados reais do sistema. Todos os arquivos CSS da V54.1.5 são preservados. O novo CSS R3 é carregado por último, permitindo comparação e ajuste incremental.

**Preservado:** logo horizontal original, navegação superior (sem sidebar fixa), Central de Atendimento com três colunas no desktop, serviços, controllers, banco de dados, lógica de agendamento, check-in, fila inteligente, IA e revisão humana, Gmail, Evolution API, SMS, Brevo SMTP, WebRTC/teleconsulta, APK e formulários existentes.

## Mudanças concretas

- **Paciente — `Home/Index.cshtml`:** painel autenticado refeito com cabeçalho pessoal compacto, próxima consulta dinâmica em destaque, status, quatro etapas da jornada, atalhos diretos, solicitações recentes e informações do cuidado. A área pública do site foi mantida intacta. A opção de modo fácil foi preservada.
- **Médico — `MedicoPainel/Index.cshtml`:** próximo atendimento e roteiro das tarefas agora compartilham a faixa principal; agenda, progresso do dia e gerenciamento de horários permanecem conectados aos dados reais.
- **Admin/atendente — `FuncionarioPainel/Index.cshtml`:** primeira faixa de decisões com mensagem contextual e três caminhos operacionais. Permaneceram KPIs, agenda e os painéis de gestão/recepção. Atalhos originais foram preservados.
- **Recepção — `RecepcaoPendencias/Index.cshtml`:** tela reescrita em blocos claros, quatro métricas dinâmicas, fila de exceções em cartões e coluna de próximas ações, sem alterar consulta de dados ou ações de gravação. O check-in continua automático conforme a regra já implementada.
- **Páginas internas:** faixas de orientação/atalhos com contexto específico adicionadas diretamente a agenda, agendamento, histórico, jornada, teleconsulta, fila de espera, Central, solicitações, salas, confirmações, pacientes, profissionais, administração, convênios, relatórios, integrações, auditoria e outros módulos. Os atalhos das telas compartilhadas respeitam o perfil do paciente, sem levá-lo a páginas administrativas.
- **CRUD:** partial `_R3FormGuide.cshtml` para telas de criar, editar, detalhar e excluir pacientes, profissionais, funcionários, especialidades e disponibilidades (somente equipe). Não altera envio de formulário.
- **Componentes globais:** `nova-70-evolution-r3.css` refinou estrutura dos painéis, cartões, tipografia, tabelas, botões, estados vazios, barras contextuais, responsividade e foco. Nenhum CSS anterior foi apagado.
- **PWA:** nova versão do cache com inclusão do CSS R3, sem armazenamento de páginas clínicas autenticadas.
- **Marcador HTML:** `v54.1.5-evolution-r3` em `_DocumentHead.cshtml` para conferir o deploy.

## Escopo real da entrega

- **37 views de conteúdo** foram alteradas diretamente, além de **4 partials/arquivos de layout** e arquivos de estilo/cache.
- As demais views receberam os estilos e comportamentos compartilhados. **Não afirmamos que todas as 110+ telas foram individualmente redesenhadas ou testadas**. O CSV `COBERTURA-VISUAL-EVOLUTION-R3.csv` detalha a cobertura por arquivo.
- Nenhum arquivo `.cs`, controller, serviço, modelo, entidade, banco de dados, migração, configuração OAuth ou APK foi modificado.

## Verificações locais realizadas

1. Comparação de arquivos contra o projeto R2: 41 arquivos existentes alterados + 2 novos arquivos de código; nenhuma exclusão, nenhum arquivo de backend alterado. (Os arquivos de documentação adicionados depois são separados.)
2. Os 8 CSS foram analisados com `tinycss2` sem erros de parsing.
3. Os 28 arquivos JavaScript (incluindo service worker) passaram no `node --check`.
4. Renderizações estáticas do **CSS real da R3** em 1440 px e 390 px para **paciente, médico, administrador e recepção**, sem overflow horizontal. Os problemas detectados de layout mobile de médico/administrador foram corrigidos e a renderização foi repetida.
5. Os novos links `asp-controller`/`asp-action` foram verificados contra nomes de controllers encontrados no projeto (sem controllers inexistentes detectados).
6. Integridade ZIP a verificar durante o empacotamento.

**Limites:** as prévias usam HTML representativo e dados ilustrativos, não são capturas de navegador autenticado. Não foi possível executar `dotnet restore`/`dotnet build` neste ambiente, portanto as Razor views e os fluxos de gravação ainda requerem compilação e teste em staging. Testes de vídeo real, envio de canais e autenticação pós-deploy não foram executados.

## Como testar sem colocar produção em risco

1. Preserve backup da V54.1.5 original; crie branch `ui-v5415-r3` e um serviço Render de staging.
2. Abra o ZIP **completo** ou aplique o **patch R3 sobre a R2**. **Não aplicar sobre V57**.
3. Execute `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release` com o SDK indicado no `global.json` da solução. Corrija qualquer erro de Razor antes de subir.
4. Navegue com os quatro perfis em desktop e celular. Inspecione especialmente: paciente (agendamento/jornada), médico (disponibilidade e vídeo), atendente (Central e pendências), administrador (indicadores e permissões). Teste também Create/Edit/Details/Delete sem persistir alterações na produção.
5. Confirme no console: `document.querySelector('meta[name="callmed-ui-version"]')?.content` deve retornar `v54.1.5-evolution-r3`.
6. Execute teste funcional dos serviços em staging; canais temporariamente desativados não devem ser confundidos com falhas do CSS.

## Reversão

A maneira segura de restaurar a versão anterior é voltar os arquivos modificados ao commit/ZIP da R2. Apenas remover `nova-70-evolution-r3.css` reverte o novo acabamento, **mas não** a reorganização de HTML nas views. Para retorno integral, restaurar também Home, MedicoPainel, FuncionarioPainel, RecepcaoPendencias, os demais arquivos `.cshtml` modificados e o service worker.

## Observação sobre Gmail

A falha HTTP 400 na renovação do token OAuth do Gmail identificada anteriormente **não foi alterada** nesta entrega visual. A correção exige diagnóstico da autorização/refresh token e não deve ser confundida com uma correção de UX/UI.
