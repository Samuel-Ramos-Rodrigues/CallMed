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
