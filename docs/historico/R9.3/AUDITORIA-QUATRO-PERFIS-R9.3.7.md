# Auditoria complementar de interfaces — quatro perfis

**Data:** 23/11/2026. **Site examinado:** https://callmed.onrender.com/ (R9.3.3 no momento do teste). **Código corrigido:** R9.3.7, testado em Chromium local, ainda sem deploy.

## Evidência principal

O diálogo «Limpar mensagens da tela» é compartilhado pelo `feedback.js`. No site publicado, o tema escuro apresenta corpo azul escuro e **faixa de botões clara** em páginas do Médico; o defeito aparece no componente global, não só na IA. A R9.3.6 já corrige a faixa; a R9.3.7 estende a verificação aos outros avisos e diferencia ações destrutivas.

## Cobertura autenticada em produção

| Perfil | Páginas visitadas nesta rodada | Observação verificável |
|---|---|---|
| Paciente | `/Consulta/Create`, `/Consulta`, `/Jornada?modo=online`, `/ListaEspera`, `/MinhaConta`, `/Confirmacoes`, `/HistoricoExames` | Abriu confirmação «Limpar mensagens» e cancelou sem limpar; os formulários de agendamento não foram submetidos. |
| Médico | `/MedicoPainel`, `/Jornada`, `/Salas?aba=online`, `/MinhaConta`, `/Organizacao` | Diálogo do assistente aberto em claro/escuro. Faixa clara no tema escuro reproduzida no site antigo, em mais de uma página. |
| Atendente | `/FuncionarioPainel`, `/Atendimento`, `/Solicitacoes`, `/Agenda`, `/RecepcaoPendencias`, `/Confirmacoes`, `/ListaEspera`, `/MinhaConta` | Confirmou o modal da IA em painel e pendências, sempre fechando pelo X. Teste visual escuro de todos os modais não concluído. |
| Administrador | `/FuncionarioPainel`, `/Organizacao`, `/Acessos`, `/Relatorios`, `/Configuracoes`, `/Integracoes`, `/Auditoria`, `/MinhaConta` | Páginas visitadas em tema claro. Confirmação da IA aberta/cancelada em claro e escuro; nenhum problema do rodapé do diálogo relatado nesta sessão. |

## Componentes compartilhados auditados no código

| Componente | Uso | Resultado da correção local |
|---|---|---|
| `dialog.cm-feedback-dialog` | IA, Cancelar consulta, Remover acesso, Cancelar solicitação, Sair da espera, Aprovar/descartar rascunho | Centralização e temas corrigidos desde R9.3.6. Botão destrutivo distinto em R9.3.7. |
| `.cm-feedback-banner` | Mensagens de sucesso, erro e aviso nas telas dos quatro perfis | X com área de toque 44×44 px e afastado do texto em R9.3.7. |
| `.modules-dialog` | «Mais funções» em todos os perfis | Contraste e fechamento checados em 12 cenários. X ampliado. |
| `.channel-status-dialog` | Central de Atendimento/equipe | Contraste, responsividade e X testados em Atendente/Admin simulados. |

## Testes proibidos/pendentes

Não foram executadas exclusões, cancelamentos, marcação de consultas, envio de SMS/WhatsApp/e-mail, abertura de prontuário ou videochamada em produção. Não se pode afirmar que todas as rotas de todos os perfis foram homologadas; para isso é necessário ambiente de testes com dados fictícios, acesso autenticado, build .NET 10 e QA exploratório após deploy.

## Diferenças entre sessões ao vivo

A sessão do **Médico** relatou a faixa de ações clara em modo escuro em mais de uma página. A sessão **Administrador** relatou o mesmo diálogo visualmente adequado no modo escuro. A divergência pode resultar de preferências/estado ou julgamento de interface: **não é prova de que todas as páginas exibem o defeito nem de que ele não existe**. A correção global da R9.3.6 independe dessa variação. A sessão do **Atendente** inspecionou o diálogo em tema claro e não concluiu alternância de tema em todas as páginas.

A auditoria automatizada descreveu elementos de interface em várias páginas, mas não equivale a testes de todos os botões, fluxo clínico real nem à matriz completa de 4 resoluções no site publicado.
