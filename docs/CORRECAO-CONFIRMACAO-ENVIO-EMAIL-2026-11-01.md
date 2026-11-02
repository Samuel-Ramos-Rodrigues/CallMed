# Confirmação de envio dos rascunhos de e-mail

## Problema reproduzido

A Central consultava os estados das mensagens a cada dois segundos e recriava
o painel de entrega mesmo quando nada havia mudado. Se a confirmação de
"Aprovar e enviar" estivesse aberta, seu formulário original era removido.
Ao confirmar, `feedback.js` identificava que ele já não estava na página e
encerrava a ação sem efetuar o POST. Uma nova tentativa podia funcionar se a
confirmação terminasse antes da próxima atualização.

O comportamento foi reproduzido no navegador com os scripts reais. Não foram
consultados registros de produção para confirmar a causa das tentativas
relatadas pelo usuário.

## Alterações

- Preserva o painel quando o estado da entrega não muda, inclusive o formulário
  renderizado pelo Razor antes da primeira atualização.
- Mantém a atualização quando há mudança real de estado, erro ou liberação da
  próxima tentativa.
- Unifica o tratamento dos formulários iniciais e daqueles inseridos pelo
  polling. Mostra "Enviando…" ou "Descartando…" após a confirmação e bloqueia
  uma segunda submissão enquanto a primeira prossegue.
- Mantém o POST nativo, a confirmação e o token antiforgery.
- Mostra "Enviada · aceita pelo servidor de e-mail" no histórico, sem confundir
  a aceitação pelo transporte com a entrega na caixa do destinatário.
- Atualiza o título da mensagem quando ela deixa de ser um rascunho.
- Atualiza o cache do PWA para v59.

## Validação

- Compilação do projeto e das verificações concluída sem erros. A compilação
  completa do aplicativo informou os dois avisos já existentes: CS8602 em
  ForgotPassword.cshtml.cs e CS8321 para CanalIcon em Atendimento/Index.cshtml.
- 198 verificações de backend aprovadas.
- Nove cenários de envio no Chromium com `feedback.js` e `atendimento.js` reais:
  confirmação após atualizações, cancelamento, andamento e bloqueio de segunda
  submissão, descarte em formulários iniciais/dinâmicos, mudança de estado por
  outra sessão e tentativa manual após falha.
- Verificações existentes de andamento da IA, conexão e seis layouts em temas
  claro/escuro aprovadas.
- Sintaxe dos scripts alterados verificada.

Os testes de navegador usam um servidor local simulado. Nenhum e-mail real foi
enviado. Publicar o pacote no Render para aplicar a correção ao site.

Para executar a nova regressão, usar `npm run test:atendimento-envio` em `tests`,
com Playwright instalado e `CHROME_PATH` quando o executável não for o padrão.
