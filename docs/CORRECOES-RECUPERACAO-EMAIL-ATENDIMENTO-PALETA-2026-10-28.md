# CallMed — correções de recuperação, respostas de e-mail e aparência clínica

## Recuperação de senha

- Os links novos levam o e-mail, o UID da conta e o token Identity serializado em Base64URL, para não corromper caracteres como `+`, `/` e `=` em query strings.
- A página identifica a conta pelo UID do link, exibe somente o e-mail mascarado e não solicita CPF novamente. Links já emitidos sem UID continuam sendo aceitos enquanto o token Identity for válido.
- O handler de redefinição remove os erros de validação da entrada inicial (`Input.Usuario`), que não é enviada no formulário de nova senha. Isso evita o aviso indevido “Informe seu CPF ou e-mail” após abrir o link.
- A solicitação continua respondendo genericamente, por segurança. Um link só é enviado para o endereço de e-mail associado à conta Identity/paciente vinculado. Digitar um e-mail que não está associado a uma conta não envia redefinição; esse comportamento evita que a página revele quais contas existem. Verifique também o painel transacional do Brevo se houver falhas de transporte.

## Respostas da Central por e-mail

- O SMTP só envia; não recebe respostas por si só. Para o `Responder` voltar à conversa da Central, configure Brevo Inbound Parsing com um domínio próprio/subdomínio e registros MX apontados para o Brevo.
- O CallMed cria um `Reply-To` assinado por conversa, aceita o webhook em `POST /api/atendimento/email/inbound`, valida o cabeçalho `X-CallMed-Webhook-Secret`, confirma a assinatura do alias e verifica se o remetente bate com a conversa antes de registrar a mensagem.
- A entrada permanece desativada por padrão. Configure no Render os valores seguintes depois de concluir a configuração DNS/webhook:

```text
Atendimento__Email__Inbound__Enabled=true
Atendimento__Email__Inbound__WebhookSecret=SEGREDO_ALEATORIO_INDEPENDENTE_COM_32_OU_MAIS_CARACTERES
Atendimento__Email__Inbound__ReplyAddressSecret=OUTRO_SEGREDO_ALEATORIO_INDEPENDENTE_COM_32_OU_MAIS_CARACTERES
Atendimento__Email__Inbound__ReplyDomain=reply.seudominio.com.br
```

- URL do webhook: `https://callmed.onrender.com/api/atendimento/email/inbound`
- Evento do Brevo: `inboundEmailProcessed`; cabeçalho personalizado `X-CallMed-Webhook-Secret` com o mesmo valor da variável `WebhookSecret`.
- Crie um subdomínio dedicado (por exemplo, `reply.seudominio.com.br`) e configure os MX indicados pelo Brevo. Não use `gmail.com` como domínio de entrada, porque você não controla esse DNS.

## Aparência e acessibilidade

- A folha global `clinic-functional-fixes.css` agora usa uma paleta sálvia mais perceptível, substituindo fundos brancos legados em painéis administrativos, filtros, tabelas, atendimento, chat e formulários.
- A tela de login é forçada a uma coluna para viewports até 960px, independentemente do tema/alto contraste. O tema de acessibilidade permanece disponível na área autenticada; a barra flutuante não aparece no login.
- Cache estático atualizado para `callmed-static-v37-email-reset-routing-clinic-palette`.

## Homologação recomendada

1. Solicitar redefinição usando um e-mail real vinculado a uma conta; abrir o link e confirmar que só aparecem nova senha e confirmação, sem campo CPF/e-mail obrigatório.
2. Confirmar que o token é válido e que o sistema aceita a nova senha; testar link expirado ou alterado e validar mensagem clara.
3. Solicitar redefinição com e-mail não cadastrado. A resposta do site deve permanecer genérica e não deve enviar mensagem.
4. Antes da entrada e-mail: testar o envio SMTP para caixa controlada. Depois de configurar DNS e webhook Brevo, responder à mensagem recebida e confirmar que aparece na mesma conversa da Central.
5. Testar a área administrativa, tabelas, painel de conversas e login em desktop, tablet e celular, incluindo tema escuro e alto contraste.

## Limites da validação

A checagem automatizada estática não substitui `dotnet build` e a homologação no Render. Não foram enviadas mensagens reais a partir deste pacote.
