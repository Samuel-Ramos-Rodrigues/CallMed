# CallMed Workspace R6 — implementação real

Base: CallMed V54.1.5 (evoluções R1–R5 mantidas para compatibilidade). Esta entrega implementa o design aprovado **nos arquivos ASP.NET Core Razor do projeto**, não é uma prévia gráfica.

## Mudanças no código

- `src/CallMed.Web/Views/Home/Index.cshtml`: nova área do paciente com compromisso principal real, quatro etapas e atalhos contextuais para agendamento, teleconsulta, exames e IA. A parte pública e o modelo de dados foram preservados.
- `src/CallMed.Web/Views/MedicoPainel/Index.cshtml`: estação clínica com sequência de horários ligada a `AgendaHoje`, painel de próximo atendimento, próximos dias e o componente original de bloqueios. Não inclui diagnósticos inventados ou informações clínicas artificiais.
- `src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml`: recepção com três grupos de prioridades reais e lista de horários; administrador com gráfico de barras de confirmações baseado nos valores do modelo, alertas e acessos de gestão. Não foram criados KPIs fictícios.
- `src/CallMed.Web/Views/Atendimento/Index.cshtml`: identificador de módulo para o design de três colunas; toda a lógica de conversa, mensagens e aprovação permanece igual.
- `src/CallMed.Web/Views/Shared/_Layout.cshtml`: classe do novo tema e exibição do assistente flutuante também na Central; permanece oculto em fluxos clínicos focados (como teleconsulta), evitando interferir com vídeo.
- `src/CallMed.Web/Views/Shared/_AssistenteFlutuante.cshtml`: assistente liberado também para médicos, preservando componente original, marca, scripts e eventos.
- `src/CallMed.Web/wwwroot/css/nova-95-workspace-r6.css`: folha nova com layout branco, navy/turquesa, componentes dos quatro perfis, melhorias das páginas internas, Central e mobile.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`: carrega o novo CSS por último e expõe marcador `v54.1.5-workspace-r6`.
- `src/CallMed.Web/wwwroot/service-worker.js`: nova versão do cache do PWA e inclusão do CSS da R6.

## Mantido

Controllers, serviços, banco de dados, integrações Gmail/Brevo/Evolution/SMS, endpoints MVC, nomes dos campos e ações de formulários, estrutura de login, logo original e scripts do assistente IA. A R6 altera apresentação, não corrige credenciais OAuth do Gmail nem modifica a lógica dos canais.

## Limitações reais

Os painéis iniciais foram redesenhados diretamente; as páginas internas passam a usar o novo cabeçalho, paleta, superfícies e acabamento pela folha compartilhada, mas **não foram todas reescritas manualmente**. Os dados exibidos na R6 vêm dos viewmodels existentes. O build .NET e os testes de ponta a ponta autenticados **não foram executados neste ambiente**; verificação de sintaxe CSS/JS e testes estáticos de integração visual foram realizados.

## Publicação segura

1. Gere uma branch de teste a partir da V54.1.5/R5; não aplique o patch sobre a V57.
2. Faça `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release` em máquina com .NET 10.
3. Publique em ambiente isolado e confira paciente, médico, atendente, administrador, Central, IA, formulários, tema escuro, mobile e integração de vídeo.
4. Para conferir a versão no navegador, execute `document.querySelector('meta[name="callmed-ui-version"]')?.content` — esperado: `v54.1.5-workspace-r6`.
5. Para rollback visual, remova a referência ao `nova-95-workspace-r6.css` e restaure as views originais usando o patch inverso ou o ZIP R5 anterior. O ZIP completo R6 deve ser tratado como candidato a homologação, não como deploy já validado.
