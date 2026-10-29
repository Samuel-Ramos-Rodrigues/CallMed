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

Esta configuração envia e-mails. O recebimento automático de respostas na Central é suportado separadamente pelo Brevo Inbound Parsing, mas fica desativado por padrão até que o subdomínio, os MX e o webhook sejam configurados conforme a seção abaixo.

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

## Respostas por e-mail dentro da Central de Atendimento (opcional)

O envio SMTP por si só não recebe respostas. Para que o paciente clique em **Responder** no próprio e-mail e a resposta apareça na conversa certa do CallMed, a aplicação suporta o **Inbound Parsing** do Brevo. Isso exige um domínio/subdomínio controlado pela clínica; um endereço Gmail (`@gmail.com`) sozinho não permite configurar os registros MX que encaminham as respostas para o CallMed.

1. Controle um domínio próprio e autentique-o no Brevo. Crie um subdomínio dedicado para entrada, por exemplo `reply.seudominio.com.br`; ele deve ser distinto do domínio de envio.
2. No DNS do subdomínio, cadastre os MX documentados pelo Brevo:
   - Prioridade `10`: `inbound1.sendinblue.com.`
   - Prioridade `20`: `inbound2.sendinblue.com.`
3. No Brevo, crie um webhook com tipo `inbound`, evento `inboundEmailProcessed`, domínio do subdomínio de recebimento e URL:
   `https://callmed.onrender.com/api/atendimento/email/inbound`
4. Configure no webhook o cabeçalho personalizado `X-CallMed-Webhook-Secret` com o mesmo valor de `Atendimento__Email__Inbound__WebhookSecret` no Render.
5. No Render, configure os valores abaixo. Use dois segredos aleatórios independentes, cada um com pelo menos 32 caracteres; nunca reutilize a senha SMTP.

```text
Atendimento__Email__Inbound__Enabled=true
Atendimento__Email__Inbound__WebhookSecret=SEGREDO_ALEATORIO_DO_WEBHOOK_COM_32_OU_MAIS_CARACTERES
Atendimento__Email__Inbound__ReplyAddressSecret=OUTRO_SEGREDO_ALEATORIO_COM_32_OU_MAIS_CARACTERES
Atendimento__Email__Inbound__ReplyDomain=reply.seudominio.com.br
```

O CallMed gera um `Reply-To` exclusivo, assinado e vinculado à conversa em cada e-mail enviado pela Central. Ao receber uma resposta, valida o segredo do webhook, valida o endereço de roteamento e confirma que o remetente corresponde ao e-mail da conversa antes de gravar a mensagem. Mensagens para endereços não gerados pelo CallMed são ignoradas; o webhook nunca cria uma conversa nova apenas com base no e-mail recebido.

A integração permanece desativada por padrão. Sem domínio próprio e sem os registros DNS de entrada, o envio de saída continua funcionando, mas as respostas não retornarão automaticamente à Central.
