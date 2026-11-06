# 2026-11-06 — Android nativo 23.2.0

- Conversa atual separada dos atendimentos finalizados, sem misturar mensagens antigas na tela.
- Anteriores com lista e consulta em modo de leitura, paginação de atendimentos/mensagens e isolamento por conta.
- Novo atendimento vazio, com confirmação antes de finalizar a conversa ativa; avaliação continua opcional.
- Flag legada de encerramento reconciliada com as entradas posteriores ao último fechamento; fechamento legado arquivado sem pesquisa retroativa.
- Histórico Web/app preservado além de 250 mensagens; respostas normais da IA também são revalidadas quando o atendimento muda durante a geração.
- Bolhas ajustadas ao conteúdo e ações agrupadas no menu para preservar o espaço de leitura.
- APK 23.2.0/2320; publicar o backend desta versão antes de instalar. Sem migração ou variável nova. Guia: [CHAT-POR-ATENDIMENTO.md](CHAT-POR-ATENDIMENTO.md).

## 2026-11-06 — Android nativo 23.1.1

- Corrigida a atualização atrasada do histórico: uma leitura iniciada antes do envio, encerramento ou avaliação não pode sobrescrever o resultado da ação.
- Polling com uma única leitura em andamento, evitando respostas fora de ordem em conexões lentas.
- Nova mensagem usa imediatamente o estado confirmado pelo POST; o aviso de encerramento anterior fica oculto enquanto a resposta está sendo preparada.
- Caixa de mensagem compacta, com envio ao lado, alvo de toque de 56dp e descrição para leitor de tela. Mantidos texto ampliado e entrada com múltiplas linhas.
- Testes reproduzem a falha antes da correção e cobrem reabertura com avaliação respondida ou pendente.
- APK 23.1.1/2311 com a mesma assinatura. Nenhuma alteração de regra de negócio ou endpoint do servidor.

## 2026-11-06 — Android nativo 23.1.0

- Quatro áreas: Início, Consultas, Chat e Conta; interface com temas claro/escuro e botão Leitura global.
- Agendamento por especialidade, médico, data, horário e revisão; primeira vaga, elegibilidade e remarcação preservando médico/forma de atendimento.
- Reservas e cancelamentos do app sincronizam a solicitação de atendimento e a auditoria, usando os serviços existentes.
- Texto normal/grande/extra, alto contraste, mais espaço e redução de animações; escala do sistema respeitada e campos preservados ao ajustar a leitura.
- Perfil, alteração confirmada de e-mail/senha com renovação dos tokens atuais, revogação dos outros acessos e gerenciamento de dispositivos.
- Registros de exames aprovados do próprio paciente; convite opcional antes do formulário de avaliação no chat.
- Preferências parciais compatíveis com o cliente anterior; nenhuma migração ou variável de ambiente nova.
- APK assinado 23.1.0/2310 atualizado no site, com a mesma chave anterior. Testes e limites em [validacao/ANDROID-NATIVO.md](validacao/ANDROID-NATIVO.md).

## Android nativo 23.0.0

- Novo módulo `CallMedAndroid` em Kotlin + Jetpack Compose: login de paciente, consultas, agendamento, perfil, chat e avaliação.
- API `/api/mobile/v1` com sessões bearer revogáveis e regras clínicas existentes.
- APK assinado atualizado no download do site; instruções em `docs/ANDROID-NATIVO.md`.
- Projeto TWA anterior preservado.

## 2026-11-05 — Download direto do aplicativo Android

- APK de produção 22.2.0 recebido, com pacote e assinatura conferidos.
- Página pública `/aplicativo` e download `/aplicativo/baixar`, com MIME Android, nome de arquivo e retomada por HTTP Range.
- Botão de download na página inicial e links nos rodapés; PWA preservado.
- APK e metadados incluídos na publicação do ASP.NET/Render, com exceção específica no `.gitignore` para o arquivo público.
- Cache do PWA atualizado e rota de download excluída do cache.
- Mantidos perfil, avaliações, recuperação de senha, canais e associação TWA.
- Guia: [APLICATIVO-DOWNLOAD-DIRETO.md](APLICATIVO-DOWNLOAD-DIRETO.md).

## 2026-11-05 — Perfil e encerramento com avaliação direta

- Minha Conta para todos os papéis, em três abas: Meus dados, Segurança e Preferências.
- Foto privada, edição dos próprios contatos, confirmação de senha ao mudar e-mail e sincronização do cadastro clínico com Identity.
- Troca de senha, histórico de acessos/alterações e revogação de sessões; renovação do cookie preserva o identificador de acesso.
- Preferências de tema/acessibilidade e canais permitidos para lembretes/lista de espera; padrão anterior preservado para contas antigas.
- Ferramenta da IA para sugerir encerramento, com validação no backend e avaliação direta, sem etapa de aceite do formulário.
- Novas perguntas e tomada humana durante a geração impedem encerramento obsoleto; rascunho manual de e-mail não envia convite por inferência.
- Patch aditivo V30, sem novas credenciais, e cache PWA atualizado. Correções de senha, chat e TWA preservadas.
- Guia: [PERFIL-E-ENCERRAMENTO.md](PERFIL-E-ENCERRAMENTO.md).

## 2026-11-04 — Correção da redefinição de senha

- Corrigida a validação indevida de CPF/e-mail do outro formulário (chave sem prefixo `Usuario`).
- Validação do link antes do formulário e mensagens de erro visíveis.
- Recuperação válida remove bloqueio temporário, verifica a senha recarregada do banco e apresenta confirmação explícita.
- Contas desativadas e bloqueios administrativos continuam protegidos.
- Chaves do Data Protection persistidas no PostgreSQL pelo provedor oficial; patch aditivo V29.
- Testes com Identity real, reinício do provedor, falha de gravação e fluxo HTTP completo até o login.
- Após atualizar, solicite um novo link de recuperação. Guia: [CORRECAO-REDEFINICAO-SENHA.md](CORRECAO-REDEFINICAO-SENHA.md).

## 2026-11-04 — Redução da espera no agente do site

- Saudações isoladas respondem localmente; pedidos e confirmações seguem o agente completo.
- Filtro de avaliação sem consulta para mensagens comuns do widget; polling sem resolução duplicada do paciente.
- Par de mensagens gravado uma vez na Central, com leitura do histórico V12 preservada.
- Orçamento total de 40 segundos no chat e limite no navegador; removida a repetição automática do POST após erro.
- Registros de duração para preparação, Gemini e tempo total. Mantidos avaliações, canais e ajustes do TWA.
- Guia: [DESEMPENHO-ASSISTENTE.md](DESEMPENHO-ASSISTENTE.md).

## 2026-11-04 — Avaliações ao encerrar o atendimento

- Pesquisa opcional de 1 a 5 com comentário no WhatsApp, SMS, e-mail e agente do site.
- Encerramento pela equipe, botão do site ou despedida explícita; fechar o widget não encerra a conversa.
- Respostas separadas da IA e do agendamento, com deduplicação, persistência e envios em segundo plano.
- Tela de avaliações por mês/canal com média, satisfação e comentários; patch aditivo V28 sem novas credenciais.
- Guia: [AVALIACOES-ATENDIMENTO.md](AVALIACOES-ATENDIMENTO.md).

## 2026-11-03 — Integração de SMS com httpSMS

- Substituído SMSGate por httpSMS: API Key, número From e formato from/to/content.
- Webhook CloudEvents com JWT HS256, filtro do chip e deduplicação por ID da mensagem.
- Entrada persistida antes de responder ao webhook; IA processada em segundo plano e retomada após reinicializações.
- Status de fila, envio, entrega, falha e expiração atualizados por eventos autenticados.
- Mantidos modo humano, solicitações de agendamento e histórico; demais canais conservam seus provedores.
- Guia de variáveis do Render e configuração do webhook: [HTTP-SMS.md](HTTP-SMS.md).

## 2026-10-29 — Termos de Serviço e transparência Gmail OAuth

- Adicionada a página pública `/termos-de-uso`, sem autenticação.
- Links para Termos de Serviço e Política de Privacidade adicionados aos rodapés público, de autenticação e autenticado.
- Política de Privacidade ampliada para descrever a integração Gmail da caixa de atendimento e o uso limitado de IA para rascunhos administrativos com aprovação humana.
- Incluídas instruções para configurar URLs no Google Auth Platform e ressalva sobre domínio verificado.

## Identidade visual clínica e tratamento da logo — 28/10/2026

- Introduzida a camada `clinic-theme.css` em layouts autenticados, públicos e de identidade, com paleta azul-petróleo/verde-água.
- Padronizados cartões, botões, campos, tabelas, status, mensagens de sucesso/erro/aviso e navegação.
- Refinados responsividade, foco visível, tema escuro e preferência por movimento reduzido.
- Removidos fundos aplicados por CSS às imagens de logo; os PNGs transparentes foram aparados sem alterar o desenho original.
- Atualizado o tema de cor do navegador/PWA e a versão do cache estático para incluir a nova folha de estilos.
- Apenas alterações de interface; fluxos de negócio, banco de dados e permissões não foram alterados.

[Detalhes da identidade visual](IDENTIDADE-VISUAL-CLINICA.md).

## Correção de build — validador do webhook SMS — 28/10/2026

- Restaurado `ValidarAssinaturaSmsGateway`, chamado pelo endpoint `POST /api/atendimento/sms` e ausente no código publicado.
- A validação usa o corpo bruto + timestamp, HMAC-SHA256, janela temporal de ±5 minutos e comparação em tempo constante.
- A implementação segue o formato documentado pelo SMS Gateway para `X-Signature` e `X-Timestamp`.
- O erro de compilação `CS0103` apresentado no Render é corrigido por esta alteração. A compilação ainda deve ser confirmada no CI/Render com .NET 8.

## Agendamento direto e triagem de exames — 28/10/2026

- Agendamentos feitos pela tela normal são confirmados imediatamente, sem redirecionamento para triagem por ausência de matriz de convênio.
- Convênio válido do cadastro é aplicado automaticamente; cobertura explicitamente negativa exige atendimento particular ou outra especialidade.
- Novos exames ficam pendentes até aprovação administrativa; decisões de aprovação/rejeição registram usuário, data e auditoria.
- Patch de banco V24 preserva exames anteriores marcando-os como aprovados.
- Campo renomeado para “Médico de preferência”. Ver [detalhes e homologação](MUDANCAS-AGENDAMENTO-EXAMES-2026-10.md).

## Limpeza das integrações de e-mail — 28/10/2026

- Removidos a configuração, o serviço de busca de e-mails recebidos, o endpoint e as opções do provedor anterior de entrada.
- Removidas configurações legadas e duplicadas de provedores de saída.
- Mantida uma única configuração de saída por SMTP (`Smtp:*`), com Brevo como servidor recomendado e variáveis de ambiente sem credenciais.
- O envio de recuperação de senha e da Central usa o serviço SMTP configurado.
- O recebimento automático de e-mails não está habilitado nesta versão; essa função precisa de uma integração de entrada própria se voltar a ser necessária.
- SMS via Android SMS Gateway, WhatsApp, banco de dados e login foram preservados.
- Validação estática do pacote; a compilação C# e os testes completos precisam ser executados com o SDK .NET 8.

[Guia de configuração](INTEGRACOES-BREVO-ANDROID-SMS-GATEWAY.md).

## Controle de envio Evolution — 27/09/2026

- Respeitado Retry-After após HTTP 429, com intervalo mínimo compartilhado entre envios.
- Reenvio da mesma mensagem, chave única para avisos e controle de concorrência no banco.
- Corrigida repetição de avisos da lista de espera e de lembretes após falha.
- Prazo de tentativa exibido na Central; envios parciais/incertos exigem conferência do canal.
- Patch aditivo V23 incluído; 44 verificações aprovadas. [Detalhes e atualização](CORRECAO-ENVIO-WHATSAPP.md).

## Revisão completa de CSS — 26/09/2026

- Revisadas 58 páginas Razor, duas páginas offline e os componentes compartilhados.
- Corrigidos componentes de triagem, exames, convênios, disponibilidade, relatórios, integrações, privacidade e conta.
- Ajustados celular, tema escuro, menus em tabelas, formulários offline e impressão A4.
- Seis arquivos CSS formatados e cache estático atualizado; código C# preservado nesta revisão.
- Escopo e resultados em [REVISAO-CSS.md](REVISAO-CSS.md).



## Correção — acesso do paciente aos exames — 24/09/2026

- Corrigido o acesso por “Meus exames” sem `pacienteId`: o controller identifica o paciente pela conta logada.
- Mantido o bloqueio de IDs alheios e de alterações por pacientes.
- Retirado o atalho médico genérico sem paciente; links de cada consulta permanecem disponíveis.
- Adicionados 13 testes de regressão com dados fictícios em memória. Total: 24 testes aprovados; compilação Release sem erros ou avisos.


## Layout e organização — 24/09/2026

- Nova identidade visual, navegação superior e menus por perfil.
- Painéis da equipe e do paciente, página inicial e login redesenhados.
- Estilos divididos por responsabilidade, substituindo cinco camadas antigas.
- Changelogs e relatórios antigos consolidados, com 23 arquivos removidos da estrutura ativa.
- Código C# preservado; detalhes e verificações em [LAYOUT-E-ORGANIZACAO.md](LAYOUT-E-ORGANIZACAO.md).

## V21.6.5 — Aurora Interface System
- refatoração estrutural de 56 telas navegáveis em Razor/HTML;
- novo app shell, page heroes, cards, formulários e grids de detalhe;
- redesign completo da Central de Atendimento, Identity, PWA e dashboards;
- nova identidade índigo/violeta/azul;
- nenhuma alteração nas regras de negócio C# nesta release visual.

# CallMed V21.6 — organização estrutural

- Controllers separados por domínio/área sem alterar rotas.
- DTOs, Options e ViewModels separados das entidades e controllers.
- EF Core organizado em `Data/Configurations`.
- `Program.cs` simplificado com extensions e startup initializer.
- Interfaces/Workers/Initializers organizados em subpastas.
- PWA/TWA atualizado para 21.6.0.

# Changelog

## 21.3.0 — limpeza estrutural

- consolidou a cascata autenticada em `site.css`;
- consolidou estilos públicos em `public.css`;
- consolidou estilos de autenticação em `identity.css`;
- removeu arquivos CSS antigos por número de versão do runtime;
- simplificou a lista de assets do Service Worker;
- ampliou a proteção de rotas dinâmicas contra cache indevido;
- removeu documentação histórica e scripts de limpeza já obsoletos;
- atualizou README, TWA e nomes dos artifacts do GitHub Actions;
- preservou integralmente controllers, models, services, migrations, integrações e regras de negócio da V21.2.

## V21.5 — acabamento mobile/PWA
- corrigida a topbar do paciente em celulares estreitos;
- removido o fundo branco da marca mobile e a possibilidade de logo duplicada;
- marca compacta centralizada no mobile;
- tema sai da topbar abaixo de 420 px e continua disponível em Minha Conta;
- Modo fácil continua no menu de acessibilidade `Aa`, evitando controle duplicado na Home;
- atalhos do paciente agora ficam em grade 2x2 até 480 px;
- card sem próxima consulta foi reorganizado para evitar aperto do CTA;
- barra inferior ficou mais leve e compacta, preservando área de toque;
- Service Worker atualizado para `callmed-static-v21-5-mobile-polish`;
- TWA atualizado para 21.5.0.


# Histórico consolidado das edições


---

## CHANGELOG-V21.6.1

# CallMed V21.6.1 — Evolution / WhatsApp

Correções de compatibilidade e diagnóstico da integração Evolution API.

## Alterações

- Webhook Evolution agora trata `remoteJid` em formato `@lid` e prioriza `senderPn`/`remoteJidAlt` quando houver número telefônico real.
- Extração de texto ampliada para mensagens simples, extended text, mídia com legenda, botões/listas e wrappers `ephemeral`/`viewOnce`.
- Logs estruturados de entrada, processamento e falha do webhook sem registrar o conteúdo da mensagem ou o segredo.
- Envio Evolution mantém o payload atual e permite fallback legado em 400/422 e em 500 apenas quando a resposta indica incompatibilidade de payload.
- Resposta de erro da Evolution é limitada no log para facilitar diagnóstico.
- `DbUpdateException` de mensagem externa só é ignorada quando for realmente a violação do índice único de idempotência.
- Paciente resolvido por telefone/e-mail é anexado ao EF como entidade existente antes da criação da conversa, evitando tentativa de INSERT duplicado no webhook.
- Nome padrão da instância alterado de `mksan` para `callmed`.
- TWA atualizado para `versionCode 2161` / `versionName 21.6.1`.
- Nomes dos artifacts do GitHub Actions atualizados para V21.6.1.
- Cache-busting de assets atualizado para `2161` e Service Worker para `callmed-static-v21-6-1-evolution-fix`.

## Observação

O nome configurado em `Atendimento__WhatsApp__Evolution__InstanceName` precisa corresponder ao nome real da instância criada na Evolution API. A variável de ambiente do Render continua tendo precedência sobre o valor padrão do `appsettings.json`.


---

## CHANGELOG-V21.6.2

# CallMed V21.6.2 — Parser Evolution resiliente

Correção baseada no erro real observado no Render durante o processamento de `MESSAGES_UPSERT`.

## Correções

- protege o parser estruturado do webhook contra mudanças de formato da Evolution;
- adiciona fallback genérico para extrair `remoteJid`, `senderPn`, `remoteJidAlt`, `sender`, `id`, `fromMe` e texto;
- aceita `message` como objeto, array ou JSON serializado em string;
- amplia suporte a `conversation`, `text`, `caption`, `body`, `contentText`, botões, listas e wrappers;
- mantém compatibilidade com `@lid` e prioriza JID telefônico real;
- impede que payload inesperado gere HTTP 500 apenas durante a extração;
- registra warning quando o parser principal falha e o fallback é utilizado;
- versão Android/TWA atualizada para `21.6.2` (`versionCode 2162`);
- cache PWA atualizado para `callmed-static-v21-6-2-evolution-parser-fix`.

## Observação

O processamento de negócio continua podendo retornar erro se houver falha real no banco, IA ou envio de canal. A mudança desta versão trata especificamente a etapa de interpretação do payload Evolution.


---

## CHANGELOG-V21.6.3

# CallMed V21.6.3 — Identidade segura + refatoração visual

## Segurança do atendimento por IA

- Contatos externos sem `PacienteId` continuam podendo consultar médicos, especialidades e disponibilidade, mas não podem executar ações pessoais ou mutações.
- A trava é aplicada no backend antes das tools de:
  - agendamento;
  - confirmação;
  - remarcação;
  - cancelamento;
  - lista de espera;
  - consulta de dados pessoais.
- Nova tool `identificar_paciente` valida **CPF + data de nascimento** e, somente quando ambos correspondem a um paciente ativo real, vincula a conversa ao cadastro.
- O `ConversaAtendimentoId` é inserido pelo próprio backend no contexto do agente; o modelo não escolhe a conversa que será vinculada.
- Depois da identificação, a operação sensível só continua na mensagem seguinte, quando o contexto já foi recarregado com o paciente vinculado.
- O agente recebe uma resposta determinística quando uma ação exige identificação, evitando que o modelo declare agendamento concluído sem paciente conhecido.
- Confirmações naturais como `agende`, `marque` e `faça` passam a ser entendidas como confirmação explícita quando existe uma ação válida pendente.
- Sucessos de agendamento/remarcação/confirmação/cancelamento passam a ter confirmação final produzida a partir do resultado real da tool.

## Central de Atendimento

- Conversas sem paciente exibem alerta visual claro informando que alterações estão bloqueadas.
- O polling monitora alteração de `PacienteId`; quando a conversa é vinculada, a tela recarrega o nome e contexto do paciente automaticamente.

## Refatoração visual global

- Novo design system em `wwwroot/css/design-v2163.css`.
- Redesign de sidebar, topbar, cards, KPIs, tabelas, formulários, botões, estados e responsividade.
- Central de Atendimento recebeu novo espaçamento, hierarquia, mensagens, filtros, painel de contexto e melhor comportamento em tema escuro.
- Área do paciente, páginas administrativas, login/cadastro e site público usam a mesma linguagem visual.
- Tema claro/escuro preservado.
- PWA inclui o novo CSS no cache estático e usa novo nome de cache para evitar CSS antigo após deploy.

## Android / PWA

- `versionCode`: `2163`
- `versionName`: `21.6.3`
- cache do Service Worker: `callmed-static-v21-6-3-identity-design`


---

## CHANGELOG-V21.6.4

# CallMed V21.6.4 — Indigo Horizon

Atualização visual completa baseada na V21.6.3, mantendo a correção de identidade para canais externos e todas as regras de negócio existentes.

## Nova identidade visual

- Tema principal alterado de verde para **azul/índigo**, alinhado à logo CallMed.
- Paleta principal: índigo, azul e ciano, com variantes próprias para tema claro e escuro.
- Sidebar redesenhada com fundo navy, navegação mais clara e estado ativo em gradiente azul/índigo.
- Topbar com efeito translúcido, melhor hierarquia e busca integrada.
- Cards, KPIs, formulários, tabelas, badges, menus e popovers receberam novo acabamento.
- Estados de foco ganharam destaque acessível em azul.
- Dashboard ganhou cards com profundidade e acentos visuais sem poluir a tela.
- Central de Atendimento recebeu novo tratamento para lista de conversas, mensagens e painel de contexto.
- Área do paciente ganhou hero, atalhos e cards com linguagem visual consistente.
- Assistente flutuante recebeu cabeçalho em gradiente e mensagens diferenciadas.
- Mobile/PWA recebeu navegação inferior translúcida e responsividade revisada.

## Páginas públicas e Identity

- Home pública redesenhada com hero mais forte, tipografia maior e cards modernos.
- Login, cadastro, recuperação de senha, logout e acesso negado utilizam a mesma identidade azul/índigo.
- Tema escuro dessas páginas foi revisado para navy/indigo.
- Tela offline e cores do PWA foram convertidas para a nova paleta.

## PWA / Android

- Novo cache: `callmed-static-v21-6-4-indigo-horizon`.
- `manifest.json` usa `theme_color: #4f46e5` e `background_color: #f5f7ff`.
- TWA: `versionCode 2164` e `versionName 21.6.4`.

## Arquivos principais

- `CallMedCrud/wwwroot/css/design-v2164.css`
- `CallMedCrud/wwwroot/css/site.css`
- `CallMedCrud/wwwroot/css/public.css`
- `CallMedCrud/wwwroot/css/identity.css`
- `CallMedCrud/wwwroot/offline.html`
- `CallMedCrud/wwwroot/manifest.json`
- `CallMedCrud/wwwroot/service-worker.js`
- `CallMedCrud/Views/Shared/_Layout.cshtml`


---

## CHANGELOG-V21.6.5

# CallMed V21.6.5 — Interface System Refactor

## Objetivo
A V21.6.5 refatora a experiência visual em nível de **Razor/HTML e CSS**, substituindo a estratégia anterior de apenas sobrepor estilos globais.

## Alterações
- novo app shell `cm25` com sidebar, topbar e área de conteúdo revisadas;
- cabeçalhos de página estruturados com contexto, ícone e ações;
- telas CRUD migradas para painéis, grids de detalhe e barras de ação consistentes;
- formulários de paciente, médico, funcionário, especialidade e acesso reorganizados;
- telas de exclusão/desativação transformadas em confirmações de alto risco;
- Central de Atendimento recebe estrutura visual própria para lista, conversa, contexto e composer;
- dashboards, kanban, agenda, relatórios e configurações passam a compartilhar o novo design system;
- Identity e páginas públicas atualizadas para o mesmo idioma visual;
- design responsivo e dark mode revistos;
- identidade cromática: índigo, violeta e azul, sem verde como cor principal.

## Compatibilidade
As rotas, `asp-*`, models, controllers, validações e serviços foram preservados. A mudança desta release é majoritariamente de apresentação/estrutura de view.


---

## CHANGELOG-V21.6

# CallMed V21.6 — organização estrutural

- Controllers separados por responsabilidade, mantendo as rotas existentes.
- DTO de integração removido de dentro do controller e colocado em `DTOs/Integracao`.
- DTOs do agente e atendimento separados dos Models e Services.
- Options de Gemini, SMTP, Evolution, SMS e e-mail centralizados em `Options`.
- Entidades EF organizadas em `Models/Entities` sem trocar seus namespaces persistidos.
- Enums e constantes de domínio separados das entidades.
- ViewModels movidos para pasta própria e separados em um tipo público por arquivo.
- Interfaces de serviços organizadas em `Contracts`.
- Hosted services organizados em `Background`.
- Initializers do banco organizados em `Services/Database/Initializers`.
- `MKSANContext` reduzido e mapeamentos EF movidos para `Data/Configurations`.
- `Program.cs` reduzido; DI, pipeline e endpoints movidos para extensions e inicialização de startup.
- Documentação separada por arquitetura, deploy, integrações e validação.
- `.editorconfig` adicionado para manter o padrão do código.
- PWA/TWA atualizado para 21.6.0.


---

## CHANGELOG-V22.1-DUALFLOW

# CallMed V22.1 — DualFlow Edition

## Objetivo
Criar uma versão alternativa da interface sem alterar a lógica de negócio, a arquitetura MVC, o banco, Controllers, Services, Models, integrações ou endpoints existentes.

## Mudanças de UX
- Sidebar desktop deixa de ser permanente e vira **drawer de módulos**.
- Nova **barra superior + ribbon contextual** para navegação principal da equipe.
- Novo **dock flutuante** com ações operacionais frequentes.
- Dashboard de funcionário deixa o modelo de KPIs/cards e passa a representar o **fluxo real do turno**: Entrada → Triagem → Horário → Confirmadas → Atendimento.
- Nova fila de atenção para pendências que podem travar o dia.
- Atendimento omnichannel representado como estado operacional em tempo real.
- Paciente recebe uma UX independente baseada em **jornada de cuidado**, com próximo passo, pedidos e suporte.
- Login apresenta duas perspectivas de acesso (Paciente / Equipe) sem duplicar autenticação; o ASP.NET Identity e as Roles continuam definindo o destino e as permissões.

## Compatibilidade
Toda a lógica V22 de agendamento, triagem, Evolution API, contingência, IA, lista de espera, lembretes, autorização e persistência foi preservada.


---

## CHANGELOG-V22.2-SHIFTBOARD

# CallMed V22.2 — Shiftboard Edition

Esta edição é uma variante de UX da V22.1, mantendo a lógica e a estrutura do backend.

## Nova perspectiva
- Sidebar deixou de ser navegação persistente no desktop e virou launcher sob demanda.
- Navegação principal por espaços de trabalho no topo: Turno, Central, Fluxo, Agenda e Pessoas.
- Dashboard refeito como quadro operacional: entrada → triagem → preparação → cuidado.
- Agenda mostrada como faixa temporal horizontal, evitando o padrão de cards/KPIs tradicionais.
- Fila de atenção separa o que exige ação imediata.
- Paciente e médico mantêm experiências específicas por papel.

## Preservado
Controllers, Services, Models, Data, DTOs, ViewModels, Middleware, Migrations, Gemini, Evolution, Neon, PWA e regras de negócio não foram reescritos por esta edição visual.

## Identidade visual médica — ícones e ilustração local

- Ampliado o sprite SVG com símbolos específicos de saúde, agendamento, exames e suporte.
- Aplicados ícones semânticos à página inicial, formulário de consulta, histórico de exames, triagem e central de atendimento.
- Adicionada ilustração SVG local, acessível e disponível offline, no hero público.
- Atualizada a versão do cache estático do PWA para incluir a nova ilustração.
- Documentadas referências de licença para Lucide (ISC) e Health Icons (CC0).

## 2026-10-28 — Identidade clínica sálvia e navegação horizontal
- Substituída a navegação lateral fixa por cabeçalho em duas faixas com navegação horizontal no desktop; a navegação móvel continua no rodapé em telas estreitas.
- Aplicada paleta verde-sálvia/grafite com superfícies coloridas suaves, sem branco chapado como fundo principal.
- Dashboard da equipe reorganizado: saudação, atalhos, tabela de próximos atendimentos, resumo real do ViewModel, pontos de atenção e ocupação da agenda.
- Padronizados alertas, erros de validação, campos, botões, tabelas, cartões, menus e status para áreas autenticadas, públicas e de identidade.
- Removido alerta de sucesso duplicado no layout compartilhado e atualizado o cache do PWA para a nova identidade.

## Correção de contraste e autenticação — 2026-10-28
- Corrigidas superfícies e texto no tema escuro para tabelas, cartões e formulários.
- Alto contraste passa a neutralizar cores fixas dos estilos antigos, no modo claro e escuro.
- Controles de acessibilidade disponibilizados na tela de login e para todos os perfis autenticados.
- Adicionada folha final de compatibilidade visual e atualizada a versão do cache do PWA.

## 2026-10-28 — recuperação de senha, respostas por e-mail e paleta clínica

- Vincula links novos de redefinição de senha ao UID da conta e usa token em Base64URL; o formulário mostra o e-mail mascarado e não pede CPF novamente.
- Remove o erro de validação residual do campo CPF/e-mail ao enviar apenas nova senha e confirmação.
- Adiciona testes unitários do alias Reply-To assinado e do segredo do webhook Brevo.
- Documenta recebimento de respostas na Central via Gmail API, com requisito de domínio próprio, MX, webhook e segredos independentes.
- Ajusta a paleta clínica para superfícies sálvia mais visíveis, padroniza a área de atendimento/admin e força a autenticação a uma coluna em telas de até 960px.
- Atualiza o cache estático do PWA para `callmed-static-v37-email-reset-routing-clinic-palette`.
