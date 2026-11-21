# Integrações — pontos localizados, testes pendentes

| Canal | Evidência de fonte | Verificação necessária |
|---|---|---|
| Evolution API | `AtendimentoWebhookController`, `EvolutionWhatsAppSender` | assinatura/segredo, deduplicação, callbacks, tempo de resposta |
| httpSMS | `HttpSmsAtendimentoService` e validador do webhook | autenticação, status de entrega, duplicatas e reenvio |
| Gmail API | `GmailInboxPollingService`, `GmailApiClient` | refresh OAuth, marcadores, idempotência, falhas de polling |
| Brevo SMTP | sender email e configuração | fila, confirmação, retentativa e falha incerta |
| Gemini | `AgenteClinicaService`, orquestrador | prompt injection, escopo administrativo, fallback, revisão humana |
| WebRTC | componentes web e Kotlin | ICE/STUN/TURN, permissão A/V, expiração e reconexão |
| PEP/FHIR | `IntegracaoPEP/`, outbox `PepEvento` | autenticação, autorização, contrato e replay |

A correção do envio IA preserva os estados fornecidos pelo serviço de transporte. Não foram feitas chamadas aos provedores.
