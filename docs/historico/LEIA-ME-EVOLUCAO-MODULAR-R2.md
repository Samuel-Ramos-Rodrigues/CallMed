# CallMed V54.1.5 — Evolução Modular R2

## Objetivo

Aprimorar **todas as páginas visuais** a partir da V54.1.5, sem reconstruir o layout ou alterar regras de negócio, banco, integração, vídeo, envio de mensagens ou o APK. A base é o projeto **V54.1.5 com Refinamento R1**, que mantém os cinco CSS originais.

Este pacote é uma **revisão de UI e navegação**, não uma afirmação de que todas as 110 views foram manualmente redesenhadas ou tiveram seus fluxos de gravação testados. Os componentes comuns são usados por todas as views; além deles, os principais painéis e a navegação contextual foram ajustados individualmente.

## Alterações realizadas

- Nova folha `src/CallMed.Web/wwwroot/css/nova-60-evolucao-modular-r2.css` carregada **depois** dos arquivos 00–50. Refinamento dos estilos compartilhados de botões, cartões, tabelas, campos, estados vazios, títulos, menus, responsividade, status e links de ação.
- `_Layout.cshtml`: marcação `data-cm-module` e `data-cm-action` no body para estilos específicos por módulo; inclusão da partial `_FluxoContextual` após breadcrumbs. Não foram substituídas estruturas HTML das páginas.
- `_DocumentHead.cshtml`: inclui R2 com `asp-append-version` e marcador `v54.1.5-evolucao-modular-r2` para conferir deploy.
- `_FluxoContextual.cshtml` (novo): orientação de navegação em telas de jornada do paciente, profissional, recepção e gestão. Os links usam controllers/actions já existentes. Não há formulário, JavaScript ou alteração de dados.
- `Home/Index.cshtml`: jornada do paciente passa a ter quatro passos: agendar → confirmar e preparar → check-in → atendimento; continuam sendo links para telas existentes.
- `MedicoPainel/Index.cshtml`: quatro atalhos diretos à jornada, consulta, teleconsulta e gerenciamento de horários, sem alterar agenda/consulta.
- `FuncionarioPainel/Index.cshtml`: administrador tem quatro entradas rápidas para exceções, indicadores, encaixes e integrações; o atendente mantém o fluxo operacional existente.

## Cobertura por perfil

**Paciente:** home, agendamento, histórico, jornada, lista de espera, check-in, solicitações, exames, minha conta, acessos e comprovantes. Componentes globais e orientação contextual nas rotas relevantes.

**Médico:** dashboard, jornada, consultas, horários, salas/teleconsultas, dados pessoais, acesso e fluxos clínicos preservados. Atalhos de ação no dashboard e orientação contextual.

**Atendente:** painel, agenda, recepção, confirmações, solicitações, central, fila de espera, salas, cadastro de pacientes e consultas. Prioridades existentes mantidas; orientação contextual para rotas diárias.

**Administrador:** painel, organização, profissionais, equipe, acessos, convênios, relatórios, indicadores, integrações, configurações, auditoria e demais cadastros. Priorização no dashboard e orientação contextual em áreas administrativas.

**Comum:** páginas de login/Identity, conteúdo público, suporte, formulários de criação/edição, listagens, confirmações de exclusão, tabelas, mensagens, impressão e telas de exceção continuam usando a estrutura original, com a folha compartilhada R2.

## Importante sobre os fluxos

- **Check-in:** os links apontam à jornada existente; não foi criado check-in automático novo.
- **Fila dinâmica:** atalhos levam à fila já implementada; o algoritmo não foi alterado.
- **Vídeo:** atalhos levam à tela de salas e jornada; nenhuma parte do WebRTC ou do APK foi alterada.
- **Atendimento / IA:** a Central permanece em três colunas no desktop. A regra de aprovação humana e os serviços de canais foram preservados.
- **Gmail OAuth:** falha HTTP 400 identificada em auditoria anterior é de integração; não foi modificada nesta versão visual.

## Validação realizada

- Todos os arquivos CSS da solução e a folha R2 passaram na análise sintática `tinycss2`.
- Foram verificadas a existência dos controllers/views de destino para os novos links.
- Prévia estática gerada com CSS **extraído deste pacote** e dados ilustrativos para paciente, médico, administrador e Central em 1440 px e 390 px, sem transbordamento horizontal.
- A Central foi validada com apresentação estática; não foi executada a lógica dinâmica de mensagens.
- A solução **não foi compilada com .NET** nesta sessão (SDK indisponível). Todas as rotas autenticadas e ações de banco exigem homologação em staging. Nenhum commit/deploy foi enviado ao GitHub ou Render.

## Como testar (recomendado)

1. Fazer backup da branch de produção V54.1.5 e criar branch `ui-v5415-r2`.
2. Aplicar o ZIP completo ou somente o patch em uma cópia da V54.1.5 R1 (não sobre V57).
3. Executar `dotnet restore CallMed.sln`, `dotnet build CallMed.sln -c Release` e os testes existentes.
4. Publicar em serviço de **teste**, não em produção.
5. Abrir paciente, médico, atendente e administrador; validar links da navegação contextual, campos, agenda, filtros, check-in, Central, teleconsulta e telas secundárias.
6. Testar desktop e celular com o navegador real, incluindo tema claro/escuro, tamanho de fonte e acessibilidade.
7. No console, a versão deve retornar `v54.1.5-evolucao-modular-r2` em `document.querySelector('meta[name="callmed-ui-version"]')?.content`.

## Reversão

Para desfazer apenas a interface R2:

1. Remover o `<link>` para `nova-60-evolucao-modular-r2.css` de `_DocumentHead.cshtml`.
2. Remover a linha `<partial name="_FluxoContextual" />` e os atributos `data-cm-module`, `data-cm-action` de `_Layout.cshtml`.
3. Reverter `Home/Index.cshtml`, `MedicoPainel/Index.cshtml`, `FuncionarioPainel/Index.cshtml` usando a V54.1.5 R1 original.
4. O novo CSS e a partial podem ficar no repositório sem efeitos se as referências forem removidas.

## Testes pendentes

Compilação Razor, conexão ao banco, agendamento persistido, testes reais de vídeo, envio de mensagens, validação final de todas as rotas e responsividade em dispositivo real.
