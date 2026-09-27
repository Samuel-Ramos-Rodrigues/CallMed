# CallMed — revisão completa de CSS

Revisão concluída em 26/09/2026. Base: projeto com layout novo e correção do acesso a exames.

## Escopo

Revisadas as 58 páginas Razor completas, duas páginas HTML offline e os componentes compartilhados que elas utilizam. As seis folhas de estilo foram organizadas e formatadas. As regras de negócio e a correção de autorização dos exames permanecem iguais à versão anterior.

| Arquivo CSS | Responsabilidade |
|---|---|
| `site.css` | Tema, navegação, componentes compartilhados, painéis e responsividade |
| `modules.css` | Agenda, consultas, pacientes, médicos, triagem, exames, relatórios, integrações e conta |
| `public.css` | Página pública e privacidade |
| `identity.css` | Acesso, cadastro, recuperação e saída |
| `contingencia.css` | Contingência e tela offline |
| `comprovante.css` | Impressão em A4 |

## Correções realizadas

| Área | Correção |
|---|---|
| Formulários e detalhes | Espaçamento, cartões, títulos, descrições, campos somente leitura e ações consistentes |
| Solicitações e triagem | Cinco colunas no quadro; checklist, histórico, indicadores, botões e estado encerrado |
| Convênios e disponibilidade | Regras, situação, dias da semana, intervalos, exceções e remoção de itens |
| Pacientes e exames | Cabeçalho, registros, histórico, estado vazio e ações alinhadas ao perfil |
| Relatórios | Título da página, indicadores, barras por canal e lista por especialidade |
| Integrações e atendimento | Cartões, informações de conexão, conversas, avisos e estados sem vínculo |
| Conta e autenticação | Prévia de aparência, descrições dos controles e página de saída |
| Privacidade | Estilos movidos para a folha realmente carregada pelo layout público |
| Celular | Cabeçalhos, ações, cartões e grades adaptados; menus de linha abrem dentro da tabela |
| Offline e impressão | Cores do tema compartilhado, formulários legíveis e impressão independente do tema escuro |
| Cache | Nova versão do cache estático para distribuição dos estilos atualizados |

## Verificação realizada durante a revisão

- 83 cenários de páginas, perfis e estados, em 1440 e 390 px, nos temas claro e escuro: **332 renderizações**.
- Todas as respostas foram HTTP 200, sem erros JavaScript ou rolagem horizontal da página. Tabelas, calendário e quadro conservam suas próprias áreas de rolagem.
- **23 verificações de interação**: menu e foco, tema persistente, modo fácil, assistente, senha, etapas do cadastro, seleção de agendamento, navegação móvel, seis telas em 320 px, triagem normal/encerrada, exames do paciente, relatórios, menu da tabela, horários semanais, impressão e bloqueio da fila offline.
- Comprovante renderizado em uma página A4, com texto escuro e ações ocultas, inclusive com tema escuro ativo.
- Compilação Release e **24 verificações de código aprovadas**, incluindo os 13 testes da correção do histórico de exames.

A revisão visual usou as views do projeto com dados fictícios em ambiente local. Respostas de disponibilidade foram simuladas. Esses resultados não representam validação do banco de produção ou das integrações externas. Nenhuma publicação ou envio real de mensagens foi feito. Os números acima registram a execução da revisão; capturas e logs temporários completos não integram este pacote.

Também foram exercitados estados vazios e variações de perfil em painéis, atendimento, exames, confirmações e agendamento; triagem sem vínculo e encerrada; e primeiro acesso médico.

## Conferência do pacote final

Após recompor o pacote, foram repetidas a compilação Release (zero erros e avisos), as 24 verificações de código e 60 renderizações das 15 telas e estados principais nos dois temas e larguras. Os registros dessa conferência estão em `docs/validacao/css/`.

## Inventário página por página

Todos os arquivos abaixo foram incluídos na revisão. Componentes parciais e layouts foram conferidos nas páginas que os utilizam.

| Página | Resultado |
|---|---|
| `Views/Agenda/Index.cshtml` | Revisada |
| `Views/Atendimento/Index.cshtml` | Revisada |
| `Views/Atendimento/Nova.cshtml` | Revisada |
| `Views/Auditoria/Index.cshtml` | Revisada |
| `Views/Configuracoes/Index.cshtml` | Revisada |
| `Views/Confirmacoes/Index.cshtml` | Revisada |
| `Views/Consulta/Create.cshtml` | Revisada |
| `Views/Consulta/Details.cshtml` | Revisada |
| `Views/Consulta/Edit.cshtml` | Revisada |
| `Views/Consulta/Index.cshtml` | Revisada |
| `Views/Consulta/Remarcar.cshtml` | Revisada |
| `Views/Convenios/Index.cshtml` | Revisada |
| `Views/Disponibilidade/Create.cshtml` | Revisada |
| `Views/Disponibilidade/Delete.cshtml` | Revisada |
| `Views/Disponibilidade/Details.cshtml` | Revisada |
| `Views/Disponibilidade/Edit.cshtml` | Revisada |
| `Views/Disponibilidade/Index.cshtml` | Revisada |
| `Views/Disponibilidades/Index.cshtml` | Revisada |
| `Views/Especialidade/Create.cshtml` | Revisada |
| `Views/Especialidade/Edit.cshtml` | Revisada |
| `Views/Especialidade/Index.cshtml` | Revisada |
| `Views/Funcionario/Create.cshtml` | Revisada |
| `Views/Funcionario/Delete.cshtml` | Revisada |
| `Views/Funcionario/Details.cshtml` | Revisada |
| `Views/Funcionario/Edit.cshtml` | Revisada |
| `Views/Funcionario/Index.cshtml` | Revisada |
| `Views/FuncionarioPainel/Index.cshtml` | Revisada |
| `Views/HistoricoExames/Index.cshtml` | Revisada |
| `Views/Home/Error.cshtml` | Revisada |
| `Views/Home/Index.cshtml` | Revisada |
| `Views/Home/Privacidade.cshtml` | Revisada |
| `Views/Integracoes/Index.cshtml` | Revisada |
| `Views/ListaEspera/Create.cshtml` | Revisada |
| `Views/ListaEspera/Index.cshtml` | Revisada |
| `Views/Medico/Create.cshtml` | Revisada |
| `Views/Medico/Delete.cshtml` | Revisada |
| `Views/Medico/Details.cshtml` | Revisada |
| `Views/Medico/Edit.cshtml` | Revisada |
| `Views/Medico/Index.cshtml` | Revisada |
| `Views/MedicoAcesso/Gerenciar.cshtml` | Revisada |
| `Views/MedicoPainel/Index.cshtml` | Revisada |
| `Views/MinhaConta/Edit.cshtml` | Revisada |
| `Views/MinhaConta/Index.cshtml` | Revisada |
| `Views/Paciente/Create.cshtml` | Revisada |
| `Views/Paciente/Delete.cshtml` | Revisada |
| `Views/Paciente/Details.cshtml` | Revisada |
| `Views/Paciente/Edit.cshtml` | Revisada |
| `Views/Paciente/Index.cshtml` | Revisada |
| `Views/Relatorios/Index.cshtml` | Revisada |
| `Views/Solicitacoes/Contingencia.cshtml` | Revisada |
| `Views/Solicitacoes/Create.cshtml` | Revisada |
| `Views/Solicitacoes/Index.cshtml` | Revisada |
| `Views/Solicitacoes/Triagem.cshtml` | Revisada |
| `Areas/Identity/Pages/Account/AccessDenied.cshtml` | Revisada |
| `Areas/Identity/Pages/Account/ForgotPassword.cshtml` | Revisada |
| `Areas/Identity/Pages/Account/Login.cshtml` | Revisada |
| `Areas/Identity/Pages/Account/Logout.cshtml` | Revisada |
| `Areas/Identity/Pages/Account/Register.cshtml` | Revisada |
| `wwwroot/offline.html` | Revisada |
| `wwwroot/contingencia.html` | Revisada |

## Repetir as verificações de código

```sh
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release --no-restore
dotnet run --project tests/CallMed.Checks -c Release
```

Para executar a aplicação com banco e serviços reais, configure o ambiente conforme o README. Os arquivos de ambiente de testes visuais, SDK, navegador, `bin` e `obj` não estão incluídos no ZIP.
