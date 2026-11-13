# E-mail administrativo: avisos e respostas com aprovação

## Funcionamento desta etapa

O Gmail é consultado periodicamente. Nas conversas em modo IA, mensagens novas
são analisadas e podem gerar rascunhos automaticamente. A equipe revisa e usa
"Aprovar e enviar" para enviar a resposta. A aprovação envia somente o e-mail;
não cria uma consulta nem confirma uma alteração de agenda.

A IA do e-mail continua autorizada a consultar informações públicas da clínica,
médicos e disponibilidade. Pedidos que exigem dados individuais ou alterações
são encaminhados à equipe. O agendamento, a remarcação, o cancelamento e a
confirmação de presença são concluídos pela recepção ou pelo portal autenticado.

Lembretes e avisos da lista de espera continuam com envio automático padronizado,
conforme os dados do paciente, a preferência de canal e a configuração do envio.
Os textos de e-mail agora orientam o acesso à conta CallMed ou o contato com a
recepção e esclarecem que responder ao aviso não conclui nem reserva nada
automaticamente. Os textos dos outros canais foram preservados.

A tela de revisão de rascunhos informa que aprovar envia apenas a mensagem.
O cache do PWA foi atualizado para v60.

## Validação local

- Compilação .NET 8 concluída sem erros, com os dois avisos preexistentes
  CS8602 em ForgotPassword e CS8321 em Atendimento/Index.
- 198 verificações de backend aprovadas, incluindo criação de rascunhos sem
  envio automático, bloqueio de ferramentas privadas no e-mail e controle de
  duplicidade dos avisos.
- Testes de navegador do andamento da IA e dos formulários de envio aprovados,
  incluindo os seis layouts em temas claro/escuro.
- Sintaxe dos scripts alterados verificada.

## Automação gradual

As respostas da IA continuam dependendo de aprovação nesta etapa. A liberação
de respostas administrativas sem revisão fica para depois da validação real do
fluxo e da definição dos assuntos autorizados. Os lembretes e avisos automáticos
já existentes seguem funcionando.

## Roteiro de validação após publicar

1. Confirmar que esta versão foi publicada e que as integrações Gmail, IA e
   SMTP estão configuradas. Usar um endereço e dados de teste controlados.
2. Enviar uma pergunta administrativa à caixa da clínica, em uma conversa no
   modo IA. Conferir a entrada na Central e o indicador de processamento.
3. Conferir que o rascunho aparece e que nenhum e-mail da IA foi enviado antes
   da aprovação. Revisar, aprovar e verificar o recebimento na caixa de teste.
4. Responder ao e-mail e conferir a continuidade do histórico e a nova análise.
   No modo humano, usar "Gerar rascunho" ou devolver a conversa para IA.
5. Solicitar agendamento, remarcação ou cancelamento e verificar a condução
   pela equipe/portal. Conferir que a IA não altera a agenda por e-mail.
6. Com pacientes e consultas de teste, conferir os textos dos lembretes e da
   lista de espera: o aviso não conclui confirmação nem reserva uma vaga.
7. Em ambiente de teste, verificar uma falha de integração e conferir se ela
   aparece na Central. Conferir o canal antes de repetir um envio sem confirmação.

Os testes locais usam integrações simuladas. Não substituem a conferência real
de entrega no Gmail/Brevo e de classificação do Gemini na versão publicada.
