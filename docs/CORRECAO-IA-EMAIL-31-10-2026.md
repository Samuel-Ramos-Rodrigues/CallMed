# Correção do fluxo de e-mails para IA — 31/10/2026

## Sintomas observados

- Mensagens comuns como "QUE HORAS ABRE AÍ?" e "Quero horas abre ai?" iam para "Aguardando equipe".
- "Devolver para IA" apenas mudava o campo Modo: não gerava rascunho para o e-mail já importado pelo Gmail.
- O botão não deixava claro o que havia ocorrido.

## Mudanças no código

1. `AtendimentoEmailPolitica` reconhece variantes frequentes de dúvidas administrativas públicas, mantendo bloqueio de identificação pessoal, ações operacionais e informações clínicas.
2. `AtendimentoOrquestradorService` reutiliza a geração de rascunho tanto para e-mails novos quanto para a retomada, sem enviar automaticamente. Falha/sensibilidade encaminha para a equipe com um aviso.
3. `AtendimentoConversaService.ObterUltimaEntradaPendenteAsync` só retorna a última entrada se ela não tiver uma mensagem de saída posterior; isso evita recriar um rascunho já gerado.
4. `AtendimentoController.DevolverParaIa` restaura o modo IA e tenta criar rascunho da última mensagem pendente **somente em e-mail**. Exibe resultado/erro claro depois do POST.
5. O formulário do botão usa o token antifalsificação do Form Tag Helper (sem token duplicado); `atendimento.js` deixa o submitter habilitado para não interferir no POST nativo.
6. Novos cenários em `tests/CallMed.Checks/AtendimentoEmailIaChecks.cs`.

## Segurança do fluxo

- E-mails recebidos não autentificam o paciente, mesmo quando já existe vínculo na conversa.
- Em e-mail, mensagens clínicas, solicitações de agendamento e conteúdo pessoal continuam na equipe.
- Todo texto gerado vai para `AguardandoAprovacao`. **Nenhum e-mail é enviado por essa ação**.
- Se a última mensagem for sensível, o botão retorna um aviso e mantém a equipe responsável.
- Mensagens já processadas no Gmail não são reimportadas; a retomada usa o registro local.

## Validação de deploy

1. Confirmar build e aplicação iniciando sem erros no Render (.NET 8).
2. Enviar "QUE HORAS ABRE AÍ?" de uma conta externa: conversa deve ficar em IA e criar rascunho aguardando aprovação.
3. Em conversa atualmente "Aguardando equipe" com essa mensagem pendente, clicar "Devolver para IA": o status deve mudar e aparecer novo rascunho.
4. Clicar novamente na ação não deve duplicar rascunhos (o botão já não aparece se a conversa estiver em IA).
5. Testar mensagem sensível ou "quero falar com atendente": deverá permanecer na equipe.
6. Conferir que nenhuma resposta de e-mail é enviada sem o comando "Aprovar e enviar".
7. Se houver falha do Gemini, conferir `Falha ao gerar rascunho de e-mail` nos logs e o aviso na Central.

**Limite de validação:** o ambiente que gerou este pacote não possui o SDK `dotnet`. A compilação, os testes .NET e os testes reais do Gmail, Gemini, Brevo e Render ainda precisam ser executados.
