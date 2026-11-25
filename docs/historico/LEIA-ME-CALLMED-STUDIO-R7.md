# CallMed Studio R7 — aprimoramento visual sobre a R6 / V54.1.5

## Escopo real
- Alterações de layout e componentes nas views `Home/Index.cshtml` (paciente), `MedicoPainel/Index.cshtml` (médico) e `FuncionarioPainel/Index.cshtml` (atendente e administrador).
- `Views/Shared/_Layout.cshtml`: classe CSS de identificação da R7; nenhuma alteração em rotas, formulários ou lógica do assistente IA.
- `Views/Shared/_DocumentHead.cshtml`: marcador `v54.1.5-workspace-r7` e **uma única folha CSS consolidada**.
- `wwwroot/css/callmed-studio-r7.css`: agrega em ordem os estilos originais mais o refinamento novo. Fontes CSS anteriores preservadas como material de reversão, porém deixam de ser carregadas individualmente.
- `wwwroot/css/nova-100-studio-r7.css`: fonte do novo design (consolidada no arquivo de produção acima).
- `wwwroot/service-worker.js`: versão de cache atualizada, stylesheet consolidado, mantendo proteção de rotas dinâmicas e autenticadas.

## Mudanças visuais reais
- Cabeçalho superior azul-marinho, logo original em superfície branca, navegação em abas coloridas.
- Fundo azul-claríssimo e cartões com superfícies contrastantes; hierarquia de títulos revisada.
- Paciente: destaque amplo da próxima consulta com ilustração já existente no projeto e jornada horizontal no desktop.
- Médico: horários em linha temporal com marcadores; resumo de progresso a partir de dados reais do ViewModel; foco na próxima consulta em painel escuro.
- Atendente: quatro indicadores alimentados pelo ViewModel e três colunas de prioridade mais distintas.
- Administrador: quatro KPIs reais separados; barras de resultados existentes com nova legibilidade e alertas.
- Componentes internos recebem aprimoramentos compartilhados; **as demais páginas não foram individualmente reconstruídas**.
- Assistente flutuante original preservado (sem alteração da lógica de IA), com offset no celular.

## Validação e limites
- Comparações estáticas do HTML/CSS dos quatro exemplos em 1440px e 390px: sem overflow horizontal; bolha da IA presente.
- CSS analisado sintaticamente, JavaScript do Service Worker verificado por `node --check`.
- **Não houve compilação Razor / .NET 10 nem testes autenticados de integração** neste ambiente. Use staging para revisar os quatro perfis e seus fluxos antes de produção.
- Os dados das capturas de prévia são ilustrativos e não vêm de pacientes reais.

## Publicação / rollback
- Use o ZIP completo para substituir a árvore do projeto em uma branch de teste; o patch assume **exatamente a R6** como base.
- Compile `dotnet build CallMed.sln -c Release` e rode os testes do seu projeto.
- Após deploy, verifique `document.querySelector('meta[name="callmed-ui-version"]')?.content` => `v54.1.5-workspace-r7`.
- Para rollback, restaure a R6 integralmente (evite misturar R6 e R7).
