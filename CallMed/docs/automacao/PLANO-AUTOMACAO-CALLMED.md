# CallMed — levantamento e plano de automação (V35 → V36)

Estado: auditoria estática da `master` e implementação inicial isolada na branch `feature/automacao-checkin-teleconsulta-v36`. O documento distingue comportamento existente de propostas; não atesta funcionamento dos provedores reais no Render.

## Princípios

1. Automatizar tarefas operacionais repetitivas, mas nunca consentimento clínico, identificação presencial sem prova, verificação de convênio/pagamento quando necessária ou decisão profissional.
2. Separar **confirmação de intenção**, **pré-check-in digital**, **chegada física comprovada**, **presença na sala virtual** e **atendimento iniciado**.
3. Tarefas assíncronas precisam de idempotência, registro de tentativas e resultados, monitoramento, limites de repetição e recuperação após reinicialização.
4. Toda automação tem uma exceção humana clara. Não enviar mensagens a pacientes reais em testes.
5. Preservar httpSMS, Evolution, Gmail API/Brevo, regras de autorização, banco e fluxo das quatro contas.

## Inventário confirmado no código

| Domínio | Já implementado | Arquivo observado | Lacuna / decisão |
|---|---|---|---|
| Agenda | Renovação diária de disponibilidades | `AgendaRenovacaoBackgroundService.cs` | Tornar visível o estado da última execução |
| Lembretes | Job 5 min, janelas ~24h e ~2h, avisos e preparação | `LembreteConsultaBackgroundService.cs` | Medir falhas de entrega e os casos sem destino |
| Encaixes | Job 30s, seleção com locks, ofertas com expiração e notificação | `ListaEsperaBackgroundService.cs` + `EncaixeService` | Acompanhar oferta até aceite, expiração e reposição sem operador |
| E-mail | Gmail API polling + retomada automática em 30s | `GmailInboxPollingService.cs`, `AtendimentoEmailAutomaticoService.cs` | Unificar indicadores e política de escalonamento humano |
| WhatsApp | Webhook e resgate de pendências a cada 2s | `WhatsAppPendingBackgroundService.cs` | Garantir observabilidade de erros sem reprocessar mensagens entregues |
| SMS | httpSMS, persistência e resgate a cada 2s | `HttpSmsAtendimentoService.cs` | Preservar provedor e distinguir aceitação de entrega |
| Teleconsulta | Entrada autenticada e sessão de presença | `TeleconsultaPresencaService.cs` | Propagar check-in digital somente quando pré-cadastro já foi conferido |
| Jornada | Check-in do paciente, validação presencial pela equipe, consentimento clínico | `JornadaService.cs` | Chegada presencial autônoma exige prova de presença, não só login |
| Infraestrutura | Jobs embedded/external/disabled | `CallMedJobMode.cs` | Evitar duplicidade de workers e monitorar liveness/retries |

## Primeiro incremento (nesta branch)

**Teleconsulta: check-in digital pelo evento de entrada autenticada**.
Quando o paciente realmente entra na sala virtual autorizada, o serviço:
- mantém a criação da sessão de presença;
- se a consulta já tiver `CadastroEm` e `CheckInEm` estiver vazio, registra `CheckInEm` e evento de auditoria;
- não marca `ChegadaEm` (chegada presencial) e não registra o início da consulta;
- não repete check-in em uma reentrada, não substitui consentimento, não dá à equipe permissão de entrar no vídeo.

A entrada segue a autorização já existente e requer o clique explícito do usuário para iniciar mídia. Se o cadastro estiver pendente, a teleconsulta continua acessível e o sistema não cria confirmação falsa.

Teste: `SalasChecks.cs` cobre ausência de pré-cadastro, registro automático após conferência e reentrada idempotente.

## Backlog priorizado

| Etapa | Automação | Estado | Critérios de aceite |
|---|---|---|---|
| P0 | Check-in online associado à presença autenticada | Nesta branch, testes pendentes | Sem check-in indevido, idempotente, autorização e auditoria |
| P1 | Check-in presencial via QR dinâmico ou totem | Proposta | QR rotativo da unidade, conta autenticada, prevenção de replay, confirmação presencial, exceção manual |
| P1 | Tela única de pendências operacionais | Proposta | Atendente vê apenas falhas, pré-cadastros incompletos, convênio/documento e chegadas assistidas |
| P1 | Painel de saúde das automações | Proposta | Última execução, atrasos, tentativas, falhas e modo embedded/external explícito, sem segredos |
| P1 | Confirmações/respostas ativas nos canais | Parcialmente implementado | Canal identifica consulta de forma segura, oferece link autenticado, sem confirmação por texto ambíguo |
| P1 | Ciclo de vaga: oferta → aceite/recusa/expiração → próximo candidato | Parcialmente implementado | Sem dupla reserva, sem repetir comunicação, histórico rastreável |
| P2 | Pré-cadastro orientado, com pendências automáticas | Proposta | Preenchimento reaproveitado, paciente confirma seus próprios dados e pode corrigir |
| P2 | Alertar médico quando paciente estiver preparado/online | Proposta | Mudança de estado em tempo útil, sem expor informações clínicas à recepção |
| P2 | Encaminhamento de solicitações originadas dos canais | Proposta | Sem duplicação, vínculo de conversa/solicitação/consulta e trilha de auditoria |
| P2 | Notificações de acompanhamento após consulta e satisfação | Parcialmente implementado | Respeita preferências, finalidade administrativa e consentimento aplicável |

## Ideias discutidas para a experiência do sistema

1. Navegação por **rotina do usuário**, inspirada no HTML de referência: abas horizontais, poucas ações principais, branco e verde clínico, sem barra lateral fixa.
2. **Paciente:** início, consultas, agendar, teleconsulta e mais; check-in contextual, fila e exames quando pertinentes.
3. **Recepção:** hoje, agenda, atendimento (três colunas preservadas), teleconsultas e mais; gestão por exceção, não por cliques repetidos.
4. **Médico:** meu dia, consultas, teleconsulta, registros e mais; próxima ação evidente, registros protegidos.
5. **Admin:** visão geral, operação, indicadores e gestão; configurações técnicas em segundo nível.
6. **Teleconsulta** destacada por perfil, acesso direto à sessão; médico mantém confirmação da identidade e o consentimento é do paciente.
7. **Fila inteligente** vinculada à agenda e às remarcações, com oferta temporária e aceite voluntário.
8. **Atendimento IA** com estado explícito, indicador de processamento, distinção entre automação e rascunho/manual; WhatsApp/E-mail/SMS preservados.
9. **Agendamento → confirmação → preparação → check-in → consulta → conclusão** apresentado como uma jornada única, não páginas independentes.
10. Indicadores SENAI: absenteísmo, ocupação, tempo de recepção, confirmações, ofertas aceitas e diferença antes/depois.

## Ordem recomendada de implementação

1. Concluir e validar o check-in da teleconsulta em branch de desenvolvimento.
2. Projetar QR de chegada física com prova local verificável e alternativa acessível.
3. Implementar observabilidade de jobs, filas de erro e eventos de auditoria; validar agendamento seguro e horário da clínica.
4. Consolidar as automações de confirmação/encaixe com testes de concorrência PostgreSQL e canal simulado.
5. Reorganizar UI e navegação depois dos fluxos, preservando todas as permissões e módulos.

## Validações obrigatórias antes de merge/deploy

- GitHub Actions (.NET 10, PostgreSQL, browser, Android, certificados).
- Fluxos de paciente, atendente, profissional e admin; sessão online em dois dispositivos.
- Concorrência, reinícios, reentradas, relógio da clínica e idempotência.
- Sem envio de mensagens reais em fixtures; prova de presença presencial diferente de check-in remoto.
- Não executar migração irreversível sem ensaio em banco isolado.

## Incrementos implementados na PR #3

- **1 — Check-in online e presencial:** o registro da presença autenticada na teleconsulta faz check-in quando o pré-cadastro já está concluído. No presencial, `/checkin/terminal` fornece QR temporário de quatro minutos para o paciente autenticado confirmar a chegada; o QR não é prova física infalível, já que pode ser compartilhado. Conferir casos suspeitos continua sendo responsabilidade da equipe.
- **2 — Recepção por exceções:** `/RecepcaoPendencias` reúne itens operacionais que exigem revisão (pré-cadastro, consentimento online e presença presencial ainda não registrada). Não substitui tarefas telefônicas nem o acompanhamento médico.
- **3 — Confirmações e encaixes:** mantém-se o job de lembretes (5 min), o de fila (30 s) e os estados transacionais das ofertas; os avisos passam a orientar para ações seguras via link/portal, evitando prometer alteração de agenda por SMS/e-mail não autenticado.
- **4 — Monitoramento de jobs:** renovação de agenda e rotinas de recuperação de WhatsApp, e-mail por IA e httpSMS agora publicam seu estado no mecanismo `SaudeRotinas`, junto de lembretes, fila e PEP. A escrita de telemetria nos canais de ciclo curto foi limitada a uma vez por minuto em sucesso.
- **5 — Navegação e visual:** o catálogo foi reagru pado por tarefas em Hoje, Agenda e vagas, Atendimento, Consultas e teleconsulta, Pessoas e documentos, Indicadores e gestão, e Administração. O cabeçalho foi ajustado para cada perfil, mantendo Teleconsultas em destaque. O tema claro recebeu paleta verde e branca inspirada no protótipo, sem apagar o tema escuro, o login V34 ou a Central de três colunas.

### Roteiro rápido de aceitação sem mensagens reais

1. Acessar `/checkin/terminal` com conta da recepção e conferir QR atualizado; usar outro aparelho logado como paciente para concluir o check-in de uma consulta presencial de hoje com pré-cadastro feito.
2. Verificar que outro paciente não confirma a consulta e que um QR expirado é recusado. Após escanear duas vezes, a chegada é registrada uma única vez.
3. Entrar em teleconsulta autorizada: check-in digital aparece após o pré-cadastro, não registra chegada presencial e não autoriza automaticamente o início do médico.
4. Abrir Pendências de hoje como recepção e confirmar que pacientes com preparo incompleto aparecem, enquanto etapas concluídas deixam de gerar tarefas repetitivas.
5. Abrir Operação → Execução das rotinas e conferir os serviços configurados e o comportamento quando desativados. A ausência de envio de mensagens não significa falha do job.
6. Acompanhar vaga de encaixe fictícia até expirar e ser oferecida a outro candidato; testar aceite concorrente em banco isolado.
7. Conferir links e navegação dos quatro perfis em desktop/celular, bem como tema claro/escuro, foco, contraste e acesso à teleconsulta.
8. Conferir todos os jobs do GitHub Actions antes de fazer merge na master.

### Limites

- Novo QR depende de `PublicOrigin` HTTPS (ou `RENDER_EXTERNAL_URL` HTTPS) e de chave Data Protection persistente, já exigida pelo CallMed.
- O QR deve ficar visível somente na clínica; um código retransmitido durante a validade também pode ser usado. Para comprovação mais forte de presença, considerar totem com confirmação local ou um segundo fator vinculado à unidade.
- A saúde dos jobs registra ciclos executados, não entrega efetiva de mensagens pelo provedor. Jobs e filas ainda dependem de instância ativa e configuração correta.
- Sem migração de banco, sem mudanças nos provedores httpSMS/Evolution/Gmail/Brevo e sem alterar prontuários ou regras clínicas.
