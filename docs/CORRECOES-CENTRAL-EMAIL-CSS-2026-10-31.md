# Correções na Central de Atendimento — 31/10/2026

## Diagnósticos

1. `wwwroot/css/callmed.css`: botão de visibilidade da senha (`.password-toggle`) tinha largura fixa de 38 px para a palavra **Mostrar**, que acabava cortada em algumas telas. Foi alterado para largura de conteúdo com mínimo de 76 px e espaçamento apropriado no campo.
2. `wwwroot/js/atendimento.js`: painel de situação dos canais exigia `email.inboundEnabled`, mas `AtendimentoWebhookController.Status` não fornecia a propriedade. Corrigido em ambas as pontas; agora há estados **Configurado**, **Parcial** e **Não configurado**. São estados de configuração, não testes de conexão real com Gmail ou Brevo.
3. `Views/Atendimento/Index.cshtml` e `AtendimentoController`: quando uma conversa já está em modo IA, aparece **Verificar e-mail pendente**. A ação POST antiforgery chama a retomada existente sem reimportar e-mail, gera apenas um rascunho se houver entrada elegível e informa o resultado via TempData. "Devolver para IA" usa a mesma rotina de mensagens de feedback.
4. `AtendimentoEmailDecisaoService`: esclarecido ao classificador semântico que erros gramaticais e ausência de vínculo com paciente não devem bloquear dúvidas públicas, preservando a restrição a dados sensíveis e ações clínicas.
5. Melhorias pontuais na composição dos controles da Central e do painel de status, incluindo responsividade.

## Como validar depois da publicação

1. Abra **Situação dos canais**. Se o Gmail e o SMTP estiverem configurados, E-mail deve aparecer como **Configurado**. Caso apareça Parcial, conferir no Render (sem enviar os valores): `GmailInbox__Enabled`, `GmailInbox__MailboxEmail`, `GmailInbox__ClientId`, `GmailInbox__ClientSecret`, `GmailInbox__RefreshToken`, `GmailInbox__StartAfterUtc`, e a configuração SMTP Brevo.
2. Em conversa de e-mail em **Assistente IA**, clique **Verificar e-mail pendente**. Um rascunho novo deverá aparecer como **Aguardando aprovação** para mensagens públicas elegíveis. Nenhum e-mail deve ser enviado automaticamente.
3. Se retornar à fila humana, confira os logs do Render para linhas `Classificação de e-mail não disponível`, `Falha na decisão semântica do e-mail`, `Retomada de e-mail ... interrompida`, `Falha ao gerar rascunho`.
4. Verifique telas de Login e Cadastro e o botão Mostrar/Ocultar em celular e desktop.

## Escopo e validação

Os arquivos do banco, migrations, configurações sensíveis e regras de envio SMTP não foram alterados. CSS e JavaScript foram validados sintaticamente; o modal foi testado com API simulada em três larguras. Compilação .NET e conexões externas requerem execução no ambiente adequado.
