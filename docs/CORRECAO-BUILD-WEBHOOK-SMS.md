# Correção de build: webhook SMS Gateway

## Sintoma

O build do Render falha com `CS0103` em `Controllers/Api/AtendimentoWebhookController.cs`, informando que `ValidarAssinaturaSmsGateway` não existe no contexto atual.

## Correção

O método privado foi restaurado no controlador. Ele:

- exige corpo, assinatura, timestamp e segredo não vazios;
- aceita timestamps Unix dentro de uma janela de ±300 segundos;
- calcula HMAC-SHA256 com a mensagem `payload + timestamp` e a Signing Key configurada;
- compara a assinatura em tempo constante.

Esses passos correspondem à documentação de assinatura dos webhooks do SMS Gateway: https://docs.sms-gate.app/features/webhooks/.

## Implantação

1. Copie o arquivo corrigido `CallMedCrud/Controllers/Api/AtendimentoWebhookController.cs` para o repositório ligado ao Render, ou aplique o patch fornecido separadamente.
2. Faça commit e push para a branch configurada no Render.
3. Confirme um novo deploy e verifique que o `dotnet publish` termina sem o erro `CS0103`.
4. Depois do deploy, envie um evento de teste do SMS Gateway para validar a assinatura com a Signing Key configurada em `Atendimento__Sms__Http__WebhookSecret`.

Esta correção resolve o símbolo ausente que bloqueia a compilação. O fluxo ainda deve ser homologado com uma assinatura real enviada pelo gateway.
