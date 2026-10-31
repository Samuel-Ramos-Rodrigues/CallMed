# Auditoria de páginas CallMed — 31/10/2026

## Escopo e resultados

- Base: `CallMed-Classico-Refinamento-Geral.zip`.
- **81 arquivos Razor** inspecionados por auditoria estática de rotas e referências.
- **27 controllers**, **298 links/formulários MVC** mapeados e conferidos; nenhum método de destino ausente.
- **39 recursos estáticos** referenciados e **7 links de Razor Pages** conferidos; nenhum arquivo de destino ausente.
- **14 arquivos JavaScript** passaram em `node --check`.
- Os dois arquivos CSS passaram no parser `tinycss2` sem erros de sintaxe.
- Capturas de página estática com CSS real em **1440, 1024 e 390 px**; nenhuma rolagem horizontal da página nem erro JavaScript na demonstração.

## Correções implementadas

1. **Histórico de exames / administrador.** Em `HistoricoExames/Index` sem `pacienteId`, administrador e funcionário são redirecionados para escolher um paciente na lista. O botão da triagem deixa de navegar a um histórico sem paciente. O acesso a históricos de terceiros por conta de paciente/médico continua sujeito às regras existentes.
2. **Central de Atendimento.** Rolagem independente e confinada à lista de conversas e ao histórico das mensagens em desktop, com cabeçalho/caixa de resposta preservados. Informações do paciente permanecem na terceira coluna. Em celular, mensagens usam a rolagem natural da página para evitar vários painéis roláveis.
3. **Menu mobile.** Removido atalho duplicado de Atendimento.
4. **Logo horizontal.** Corrigida dimensão da logo original em cadastro e página de acesso negado (estava limitada a 38px).
5. **Testes de regressão adicionados** para admin/funcionário sem paciente selecionado, médico sem paciente selecionado e política Admin da triagem (não executados, exigem SDK).

## Limitações importantes

- **Não foi executado `dotnet build` nem `dotnet test`**, porque o SDK .NET não está instalado neste ambiente.
- Não foi possível iniciar o ASP.NET Core com PostgreSQL real, integrar Gmail/Brevo/Gemini nem autenticar com perfis reais.
- Não foi realizada uma inspeção visual de cada página renderizada pelo servidor: o exame de 81 arquivos Razor foi **estático**.
- Os testes opcionais em Node da área de contingência (`happy-dom`, `playwright` pacote npm) não rodaram porque as dependências npm não estavam instaladas. Não representam falha comprovada do sistema.
- Antes de publicar em produção, executar `dotnet publish` e os testes `CallMed.Checks` em ambiente .NET 8, depois validar rotas com contas de administrador, atendente, médico e paciente.

## Páginas incluídas na verificação estática

- `Areas/Identity/Pages/Account/AccessDenied.cshtml`
- `Areas/Identity/Pages/Account/ForgotPassword.cshtml`
- `Areas/Identity/Pages/Account/Login.cshtml`
- `Areas/Identity/Pages/Account/Logout.cshtml`
- `Areas/Identity/Pages/Account/Register.cshtml`
- `Areas/Identity/Pages/_ViewImports.cshtml`
- `Areas/Identity/Pages/_ViewStart.cshtml`
- `Views/Acessos/Index.cshtml`
- `Views/Agenda/Index.cshtml`
- `Views/Atendimento/Index.cshtml`
- `Views/Atendimento/Nova.cshtml`
- `Views/Atendimento/_CanalIcon.cshtml`
- `Views/Auditoria/Index.cshtml`
- `Views/Configuracoes/Index.cshtml`
- `Views/Confirmacoes/Index.cshtml`
- `Views/Consulta/Create.cshtml`
- `Views/Consulta/Details.cshtml`
- `Views/Consulta/Edit.cshtml`
- `Views/Consulta/Index.cshtml`
- `Views/Consulta/Remarcar.cshtml`
- `Views/Consulta/_Form.cshtml`
- `Views/Convenios/Index.cshtml`
- `Views/Disponibilidade/Create.cshtml`
- `Views/Disponibilidade/Delete.cshtml`
- `Views/Disponibilidade/Details.cshtml`
- `Views/Disponibilidade/Edit.cshtml`
- `Views/Disponibilidade/Index.cshtml`
- `Views/Disponibilidade/_Form.cshtml`
- `Views/Disponibilidades/Index.cshtml`
- `Views/Especialidade/Create.cshtml`
- `Views/Especialidade/Edit.cshtml`
- `Views/Especialidade/Index.cshtml`
- `Views/Especialidade/_Form.cshtml`
- `Views/Funcionario/Create.cshtml`
- `Views/Funcionario/Delete.cshtml`
- `Views/Funcionario/Details.cshtml`
- `Views/Funcionario/Edit.cshtml`
- `Views/Funcionario/Index.cshtml`
- `Views/Funcionario/_Form.cshtml`
- `Views/FuncionarioPainel/Index.cshtml`
- `Views/HistoricoExames/Index.cshtml`
- `Views/HistoricoExames/Pendentes.cshtml`
- `Views/Home/Error.cshtml`
- `Views/Home/Index.cshtml`
- `Views/Home/Privacidade.cshtml`
- `Views/Home/TermosDeUso.cshtml`
- `Views/Integracoes/Index.cshtml`
- `Views/ListaEspera/Create.cshtml`
- `Views/ListaEspera/Index.cshtml`
- `Views/Medico/Create.cshtml`
- `Views/Medico/Delete.cshtml`
- `Views/Medico/Details.cshtml`
- `Views/Medico/Edit.cshtml`
- `Views/Medico/Index.cshtml`
- `Views/Medico/_Form.cshtml`
- `Views/MedicoAcesso/Gerenciar.cshtml`
- `Views/MedicoPainel/Index.cshtml`
- `Views/MinhaConta/Edit.cshtml`
- `Views/MinhaConta/Index.cshtml`
- `Views/Paciente/Create.cshtml`
- `Views/Paciente/Delete.cshtml`
- `Views/Paciente/Details.cshtml`
- `Views/Paciente/Edit.cshtml`
- `Views/Paciente/Index.cshtml`
- `Views/Paciente/_Form.cshtml`
- `Views/Relatorios/Index.cshtml`
- `Views/Shared/Components/Confirmacoes/Default.cshtml`
- `Views/Shared/_AssistenteFlutuante.cshtml`
- `Views/Shared/_AuthLayout.cshtml`
- `Views/Shared/_DocumentHead.cshtml`
- `Views/Shared/_Layout.cshtml`
- `Views/Shared/_LoginPartial.cshtml`
- `Views/Shared/_OrientacoesConsulta.cshtml`
- `Views/Shared/_PublicLayout.cshtml`
- `Views/Shared/_ValidationScriptsPartial.cshtml`
- `Views/Solicitacoes/Contingencia.cshtml`
- `Views/Solicitacoes/Create.cshtml`
- `Views/Solicitacoes/Detalhes.cshtml`
- `Views/Solicitacoes/Index.cshtml`
- `Views/_ViewImports.cshtml`
- `Views/_ViewStart.cshtml`
