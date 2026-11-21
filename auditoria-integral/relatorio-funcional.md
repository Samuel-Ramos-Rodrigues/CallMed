# Mapa funcional — código localizado, NÃO homologação

Todas as áreas abaixo têm elementos de implementação localizados, mas foram classificadas **"implementado no código; teste de ponta a ponta pendente"** porque não houve integração real com banco/contas/provedores nesta execução.

| Módulo | Evidência (pasta ou arquivo) | Views relacionadas | Situação |
|---|---|---|---|
| Agendamento | `src/CallMed.Application/Modules/Agenda/Services/AgendamentoService.cs` | `Consulta` | Criar/Editar/Cancelar/Remarcar; exige testes de concorrência |
| Confirmações/lembretes | `src/CallMed.Application/Modules/Agenda/Services/ConfirmacoesService.cs` | `Confirmacoes` | Verificar entregas via provedores |
| Fila de espera e encaixe | `src/CallMed.Application/Modules/Agenda/Services/EncaixeService.cs` | `ListaEspera` | Provas de expiração e atomicidade pendentes |
| Pré-cadastro e check-in | `src/CallMed.Application/Modules/Jornada/` | `Checkin` | Validação física vs remota pendente |
| Teleconsulta | `src/CallMed.Application/Modules/Jornada/` | `Jornada` | WebRTC ICE/TURN, autorização e rede pendentes |
| Atendimento multicanal | `src/CallMed.Application/Modules/Atendimento/Services/AtendimentoOrquestradorService.cs` | `Atendimento` | Erro CS0103 corrigido; contratos e estados precisam teste real |
| IA Gemini | `src/CallMed.Application/Modules/Assistente/` | `Atendimento` | Aprovação e limites demandam simulação |
| WhatsApp Evolution | `src/CallMed.Application/Modules/Atendimento/Services/Canais/WhatsApp/` | `Atendimento` | Webhook e duplicidade sem testes remotos |
| SMS httpSMS | `src/CallMed.Application/Modules/Atendimento/Services/Canais/Sms/` | `Atendimento` | Status/recebimento dependem provedor |
| Gmail API / Brevo SMTP | `src/CallMed.Application/Modules/Atendimento/Services/Canais/Email/` | `Atendimento` | OAuth/polling/SMTP dependem credenciais |
| Gestão clínica e salas | `src/CallMed.Application/Modules/Agenda/` | `Salas` | Disponibilidade física pendente |
| Psicologia | `src/CallMed.Application/Modules/Psicologia/` | `Psicologia` | Sigilo especializado e permissão pendentes |
| PEP FHIR R4 | `src/CallMed.Application/Modules/IntegracaoPEP/` | `Integracoes` | Interoperabilidade administrativa parcial; contratos pendentes |
| Relatórios | `src/CallMed.Application/Modules/Gestao/` | `Relatorios` | Definição operacional de métricas pendente |
| Android Kotlin | `mobile/android/` | `Aplicativo` | Fonte e APK com mesmo versionCode; bytecode não homologado |

Os arquivos listados não são garantia de ação funcional. Mapear a chamada exata entre controlador, DI, service, EF e resposta de rede ainda exige inspeção de métodos restantes e testes.
