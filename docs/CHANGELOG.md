

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
