# httpSMS no CallMed

Esta versão substitui o SMSGate por **httpSMS**. O número remetente continua sendo o chip vinculado ao aplicativo Android. A API do provedor não substitui o plano da operadora. E-mail, WhatsApp e os demais canais preservam suas configurações.

## 1. Aplicativo e teste inicial

1. Crie a conta em https://httpsms.com/ e instale o APK indicado pela documentação oficial: https://github.com/NdoleStudio/httpsms/releases/latest/download/HttpSms.apk.
2. Vincule o celular à conta, conceda as permissões de SMS e mantenha acesso à internet e um chip capaz de enviar SMS.
3. Confirme um envio para outro número e o recebimento de uma resposta pelo painel do httpSMS.
4. Em https://httpsms.com/settings/, copie a **API Key** para a configuração do Render. Não é o usuário/senha do SMSGate.
5. Este conector usa conteúdo em texto simples na API. Não habilite criptografia de conteúdo de ponta a ponta no aplicativo; o CallMed precisa ler as mensagens para atendê-las.

## 2. Variáveis do Render

No serviço **CallMed**, configure:

```env
Atendimento__Sms__Http__Enabled=true
Atendimento__Sms__Http__BaseUrl=https://api.httpsms.com/v1
Atendimento__Sms__Http__SendPath=/messages/send
Atendimento__Sms__Http__ApiKey=SUA_API_KEY_DO_HTTPSMS
Atendimento__Sms__Http__From=+55DDDNUMERO_DO_CHIP_VINCULADO
Atendimento__Sms__Http__WebhookSecret=SUA_SIGNING_KEY_ALEATORIA_COM_32_OU_MAIS_CARACTERES
```

`From` é o número do **chip que envia**, exatamente como vinculado ao httpSMS; não é o número do paciente. Use formato internacional, por exemplo `+5511987654321` (número ilustrativo).

Remova `Atendimento__Sms__Http__Username` e `Atendimento__Sms__Http__Password`. Substitua os valores antigos de `BaseUrl`, `SendPath` e `WebhookSecret`: apontar para `api.sms-gate.app` não funciona nesta versão. Publique o projeto atualizado antes de testar pelo CallMed.

A Signing Key é um segredo escolhido por você para autenticar os webhooks, com pelo menos 32 caracteres aleatórios. Use exatamente a mesma chave no Render e no campo **Signing Key** do httpSMS. Ela é diferente da API Key.

## 3. Webhook no httpSMS

Em **Settings → Webhooks** (https://httpsms.com/settings#webhooks), crie um webhook:

- **Callback URL:** `https://callmed.onrender.com/api/atendimento/sms`
- **Signing Key:** o mesmo valor de `Atendimento__Sms__Http__WebhookSecret`.
- **Phone Numbers:** selecione o chip definido em `From`.
- **Events:**
  - `message.phone.received`
  - `message.phone.sent`
  - `message.phone.delivered`
  - `message.send.failed`
  - `message.send.expired`

O httpSMS usa `Authorization: Bearer <JWT>` assinado com HS256. Os antigos headers `X-Signature`/`X-Timestamp` do SMSGate e `?secret=` não autenticam este endpoint. O validador genérico dos webhooks de WhatsApp permanece separado.

## 4. Fluxo e estados

1. O webhook valida o JWT, o evento e o número do chip, grava a entrada no banco e responde HTTP 200. Ele não aguarda a IA.
2. Um serviço em segundo plano verifica novas entradas de SMS a cada dois segundos e usa o atendimento existente. As entradas ficam no banco e são retomadas após reinicializações dentro de uma janela de 72 horas; mensagens antigas de outros provedores não são reprocessadas.
3. Modo humano e pedidos por atendente são respeitados. Os pedidos de agendamento continuam no fluxo de solicitações do CallMed.
4. A API aceitar o SMS deixa a mensagem **na fila**. `message.phone.sent` confirma envio pelo aplicativo, `message.phone.delivered` confirma entrega e os eventos de falha/expiração final registram o erro.
5. Reenvios do mesmo webhook não duplicam a entrada. Retornos atrasados não rebaixam uma mensagem já entregue.

O prazo do webhook no httpSMS é de cinco segundos. O processamento em segundo plano evita usar esse prazo para gerar a resposta da IA. Uma instância Render suspensa pode atrasar o primeiro recebimento; os logs do provedor ajudam a distinguir timeout de erros de autenticação.

## 5. Verificação após publicar

- Em **Integrações**, confira se o SMS está configurado. Isso não comprova entrega física.
- Envie um SMS de outro número ao chip vinculado. A entrada deve aparecer na Central e receber uma resposta se a conversa estiver no modo IA.
- Assuma a conversa e envie outra mensagem: a IA deve permanecer pausada.
- Confira no histórico a mudança de fila para envio/entrega e compare com o aplicativo.
- HTTP 401 no webhook: confira a Signing Key. HTTP 401/403 no envio: confira a API Key. HTTP 422: confira `From` e destinatário. HTTP 429: confira a cota e os limites da conta.
- Se o envio ficar sem confirmação de rede, confira o painel antes de repetir: uma nova tentativa pode duplicar um SMS que o provedor já aceitou.

## Fontes e validação

- API e autenticação: https://docs.httpsms.com/
- Assinatura e prazo de webhooks: https://docs.httpsms.com/webhooks/introduction
- Eventos e campos: https://docs.httpsms.com/webhooks/events

As verificações automatizadas usam HTTP simulado e banco em memória. Nenhum SMS real é enviado por esses testes. A entrega pela operadora precisa ser validada com o aparelho e as credenciais da sua conta.
