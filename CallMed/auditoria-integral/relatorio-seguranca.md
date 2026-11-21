# Segurança — varredura inicial e limites

**Não foi feito pentest.** Não se atribuiu conformidade à LGPD/CFM/CFP/ANS.

Análise contextual executada:
- `MobilePacienteController` aplica política `MobileAuth.Policy` no controller e usa `MobileAuth.PacienteItem` para consultar registros do paciente. Isso reduz risco de exposição cruzada, mas os endpoints restantes não foram revisados integralmente.
- `AtendimentoWebhookController` valida segredo na Evolution e header `Authorization` no httpSMS. A implementação completa de replay/prova de autenticidade do provedor não foi validada.
- `ServiceCollectionExtensions.cs` persiste DataProtection no EF, aceita certificado configurado e configura cookies e bloqueio de senha. A correta proteção das chaves depende das variáveis do ambiente.
- O ZIP não contém arquivo `.jks`, `.keystore`, `keystore.properties` ou `appsettings.Production.json`.
- Scanner simples de strings longas em configurações não apontou segredo óbvio; **resultado negativo não assegura ausência de segredos**.

**Riscos a investigar, sem alegar vulnerabilidade comprovada:** objetos por ID (IDOR), CSRF/antiforgery em todos os POSTs, uploads, corpo de webhooks, vazamento em logs, chaves de links temporários, integridade de formulário psicológico, rate limiting real, recuperação da chave Data Protection, perfis no servidor.

**Restrições:** não executar exploração ou escrita contra produção. Usar dados sintéticos e contas isoladas.
