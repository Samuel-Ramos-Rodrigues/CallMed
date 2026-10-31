# CallMed Atlas — e-mail e visual, 31/10/2026

## Diagnóstico comprovado no código anterior

`AtendimentoEmailPolitica.ExigeRevisaoHumana` usava listas fixas de termos restritos e de temas públicos. Se a frase não correspondesse exatamente a temas públicos, a mensagem era transferida para `ModoAtendimento.Humano` antes de o Gemini receber a pergunta. O prompt em `AgentePrompt` também descrevia aquela lista positiva como regra do backend. Assim, o banner **“A última mensagem exige revisão humana...”** podia ocorrer mesmo para frases administrativas.

## Correção técnica aplicada

- `AtendimentoEmailDecisaoService` realiza a classificação semântica via Gemini (sem ferramentas, sem histórico de prontuário), com JSON explícito `RASCUNHO` ou `EQUIPE`.
- `AtendimentoEmailPolitica` mantém apenas o filtro de identificadores pessoais numéricos para não submeter CPFs em texto ao classificador externo.
- `AtendimentoOrquestradorService` aplica a classificação tanto a entradas novas quanto ao botão **Devolver para IA**. Se o Gemini estiver indisponível, a conversa fica segura na fila da equipe com erro discernível ao retomar.
- `AgentePrompt` foi corrigido para responder perguntas públicas consultando ferramentas oficiais, sem encaminhar por padrão.
- Gmail API continua exclusivamente para importar; Brevo SMTP continua para enviar mensagens após revisão e aprovação. Os rascunhos não são enviados automaticamente.

## Novo CSS CallMed Atlas

- `wwwroot/css/callmed-atlas.css`, carregado após o CSS-base das views para manter compatibilidade de componentes funcionais e scripts existentes.
- Navegação superior com acesso direto à Central, tipografia e paleta completamente novas (tons neutros, verde-petróleo e argila).
- Central com duas colunas (lista de conversas + conversa maior) e detalhes do paciente recolhíveis abaixo da conversa.
- Tema claro e escuro. Layout responsivo para dispositivos menores.
- O arquivo `preview-atlas.html` no material de desenvolvimento é uma referência estática; não representa uma execução do servidor ASP.NET.

## Teste recomendado após publicar

1. Verificar `Gemini__ApiKey`, `Gemini__Model`, GmailInbox e SMTP no Render.
2. Abrir uma conversa antiga bloqueada, clicar **Devolver para IA** e confirmar um novo rascunho pendente de aprovação para a última mensagem sem resposta.
3. Enviar do mesmo remetente outra pergunta pública, como “q hrs abre ai?”; confirmar entrada registrada e rascunho novo sem mudar para equipe.
4. Perguntar algo que exija dados clínicos privados ou ação não autorizada. Confirmar transferência para equipe sem responder dados pessoais.
5. Aprovar um rascunho e verificar envio real com Brevo; descartar outro rascunho e verificar ausência de envio.
6. Revisar Central, Agenda, Cadastro, Login e painel médico no desktop e celular.

**Limites da validação local:** sintaxe do CSS e do JS verificada; prévia visual estática renderizada; ZIP íntegro. O SDK .NET não está instalado neste ambiente, portanto não há build .NET confirmado. Nenhuma comunicação real, banco, Render ou credencial foi alterado.
