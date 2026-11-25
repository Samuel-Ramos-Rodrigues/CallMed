# CallMed V54.1.5 — Workspace Evolution R5

## Objetivo

Mudança expressiva de interface sem trocar as regras clínicas ou as integrações da base V54.1.5. O projeto evoluiu a partir da **R4**, que por sua vez deriva da V54.1.5. Mantivemos os controllers, serviços, banco e rotas, preservamos a marca horizontal e a Central de Atendimento em três colunas.

## Redesenho estrutural por perfil

- **Paciente — cuidado editorial:** área de boas-vindas com próximo compromisso/estado vazio, jornada de quatro momentos, serviços contextuais e solicitações com dados do modelo real. Mantido o modo fácil e todos os atalhos essenciais.
- **Médico — estação clínica:** agenda vertical por horário, ações por consulta, próximo paciente em painel próprio, progresso do dia, consultas futuras e bloco original de disponibilidade/bloqueios.
- **Atendente — quadro de prioridades:** três frentes com dados reais (confirmações, demandas, conversas), faixa operacional, próximas consultas e atalhos de recepção.
- **Administrador — sala de decisões:** painel de agenda, indicadores operacionais reais (sem valores fictícios em runtime), barras de confirmação, ações de gestão e próximos atendimentos.

## Páginas internas

- Adicionado `_R5WorkflowNav.cshtml` para oferecer caminhos de trabalho coerentes com as permissões dos quatro perfis. A navegação **não adiciona barra lateral fixa**.
- Refinada a apresentação de **agenda, agendamento, jornada, fila de espera, confirmações, salas, solicitações, integrações, relatórios, cadastro de pacientes e profissionais**, sem alterar os formulários ou operações de backend.
- O cabeçalho compartilhado, tipografia, tabelas, formulários, estados de foco, login/cadastro, página pública e componentes reaproveitados recebem a nova camada de design.
- A Central continua em três colunas com os mesmos filtros, botões e interações.

## Arquivos-chave

- `src/CallMed.Web/Views/Home/Index.cshtml`
- `src/CallMed.Web/Views/MedicoPainel/Index.cshtml`
- `src/CallMed.Web/Views/FuncionarioPainel/Index.cshtml`
- `src/CallMed.Web/Views/Shared/_R5WorkflowNav.cshtml`
- `src/CallMed.Web/Views/Shared/_Layout.cshtml`
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`
- `src/CallMed.Web/wwwroot/css/nova-90-workspace-r5.css`
- `src/CallMed.Web/wwwroot/service-worker.js`

## Construção e testes recomendados

```powershell
dotnet --version
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release
```

Depois, use uma instância **staging** com contas e dados de teste para conferir: entrar em todos os quatro perfis; agendar consulta presencial e online; Jornada e confirmação; check-in e fila de espera; entrar na sala de vídeo com dois dispositivos de teste; telas de gestão e tabelas; Central/IA; modo fácil, tema claro/escuro, notificações; 390px, tablet e desktop. Faça capturas das telas internas que apresentarem problemas.

### Verificação de versão

No Console do navegador, execute:

```js
document.querySelector('meta[name="callmed-ui-version"]')?.content
```

Valor esperado: `v54.1.5-workspace-r5`.

O Service Worker foi versionado para limpar cache estático antigo. Recarregue com `Ctrl + Shift + R` após publicar.

## Validação efetivamente realizada

- Views e rotas: todas as referências `asp-controller` dos arquivos R5 alterados apontam para controllers existentes no código.
- CSS: as **10 folhas CSS** existentes no projeto foram processadas sem erros de parsing detectados.
- JavaScript: **28 arquivos JS**, incluindo Service Worker, passaram na verificação `node --check`.
- `node tests/ux-contract.cjs`: aprovado.
- `node tests/check-psychology-pwa.cjs`: 8 verificações aprovadas.
- Telas ilustrativas renderizadas em 1440px e 390px para os quatro perfis sem rolagem horizontal indevida.
- Nenhum arquivo de backend foi editado.

**Não realizado:** build .NET/Razor (SDK indisponível no ambiente de edição), teste completo de funcionalidades autenticadas, teste efetivo de teleconsulta, aferição WCAG e homologação de todas as views. As capturas em `docs/previas-r5/` usam o CSS real com conteúdo demonstrativo, não são screenshots do site publicado. Este trabalho não garante ausência de regressões.

## Retorno seguro

Recomendamos trabalhar em uma **branch própria**. O ZIP completo contém todo o projeto. O patch é para quem já usa a **R4 exata**. Para voltar, reverta o commit/branch da R5; não apenas remova o CSS, pois há mudanças diretas no HTML/Razor das views.

## Integrações e dispositivos

Não foram modificados: Gmail OAuth, Brevo SMTP, Evolution API, httpSMS, conexão com banco, regras de IA ou aplicativo Android Kotlin. Alertas de serviços temporariamente desligados não foram classificados como erros de CSS.
