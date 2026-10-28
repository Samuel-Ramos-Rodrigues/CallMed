# Integrações de e-mail Brevo SMTP e Android SMS Gateway

## E-mail de saída — Brevo SMTP

O CallMed usa o serviço `SmtpEmailService` e a configuração única `Smtp:*` para enviar e-mails de recuperação de senha e mensagens enviadas pela Central de Atendimento. O envio não depende de SDK ou chave de API de provedor.

No Brevo:

1. Em **Settings > Senders, Domains, IPs > Senders**, cadastre e verifique o remetente.
2. Em **Settings > SMTP & API > SMTP**, obtenha o login SMTP e crie uma chave SMTP. A chave SMTP é a senha do SMTP; não use a senha de login do Brevo nem uma chave API.
3. No Render > Environment, configure:

```text
Smtp__Host=smtp-relay.brevo.com
Smtp__Port=2525
Smtp__Username=LOGIN_SMTP_BREVO
Smtp__Password=CHAVE_SMTP_BREVO
Smtp__FromEmail=REMETENTE_VERIFICADO
Smtp__FromName=CallMed | Atendimento
Smtp__EnableSsl=true
```

A porta 2525 é usada para SMTP com STARTTLS no ambiente Render. O valor de `Smtp__FromEmail` deve ser um remetente confirmado/permitido pela sua conta Brevo. Para produção, recomenda-se autenticar um domínio próprio.

Depois de definir as variáveis, faça um novo deploy. Entre como administrador em **Administração > Integrações** e use **Testar envio de e-mail (Brevo SMTP)**. O sucesso significa que o servidor SMTP aceitou a mensagem para processamento; confirme a entrega nos logs transacionais do Brevo e na caixa de entrada/spam.

### Escopo de e-mail

Esta configuração envia e-mails. O recebimento automático de mensagens de e-mail dentro da Central de Atendimento não está configurado nesta versão. O endpoint e as configurações do provedor anterior de entrada foram removidos. Se for necessário receber respostas por e-mail na Central, será preciso desenvolver e homologar uma integração de entrada separada com um provedor compatível.

## SMS — Android SMS Gateway

O CallMed mantém o envio SMS pelo Android SMS Gateway. Configure no Render as variáveis:

```text
Atendimento__Sms__Http__Enabled=true
Atendimento__Sms__Http__BaseUrl=https://api.sms-gate.app/3rdparty/v1
Atendimento__Sms__Http__SendPath=/messages
Atendimento__Sms__Http__Username=USUARIO_DO_GATEWAY
Atendimento__Sms__Http__Password=SENHA_DO_GATEWAY
Atendimento__Sms__Http__WebhookSecret=SEGREDO_DE_ASSINATURA_DO_WEBHOOK
```

Use credenciais do gateway, não as credenciais do Brevo. Configure o webhook de SMS para a rota `/api/atendimento/sms`, usando HTTPS e o segredo correspondente. Confirme no dispositivo Android que o gateway está conectado e possui permissão para enviar SMS.

## Validação

1. Configure apenas as credenciais ativas de que precisa; não coloque segredos no repositório.
2. Execute build e testes automatizados num ambiente com .NET 8.
3. Teste o envio de e-mail para um endereço controlado por você.
4. Teste SMS apenas com um número autorizado e confirme o resultado no aparelho/gateway.
5. Confirme falhas de credencial e remetente não verificado antes de publicar para pacientes.
