# Chat por atendimento — Android 23.2.0

## Fluxo do paciente

- **Chat** mostra somente as mensagens do atendimento atual.
- **Encerrar atendimento**, no menu de opções, finaliza a conversa e oferece avaliação opcional. As mensagens saem da tela atual e continuam em **Anteriores**.
- **Iniciar novo atendimento** abre uma conversa vazia. Se o atendimento atual ainda tem mensagens, o app pede confirmação antes de finalizá-lo.
- Enviar uma nova pergunta depois de encerrar também retoma o atendimento, sem exigir avaliação e sem trazer as mensagens antigas para a tela.
- **Anteriores** lista atendimentos finalizados. O paciente pode abrir cada conversa em modo de leitura e carregar mais mensagens, sem enviar no atendimento antigo.
- Campo de mensagem e envio continuam lado a lado. As bolhas curtas ajustam a largura ao texto; títulos, menu e controles respeitam a fonte do Android.

## Publicar esta versão

1. Publique primeiro o backend `CallMedCrud` deste ZIP no Render, incluindo `App_Data/Aplicativo/CallMed.apk` e `release.json`.
2. Instale o APK **23.2.0 / 2320** sobre a versão anterior. A chave de assinatura é a mesma.
3. Entre no Chat, encerre um atendimento, abra **Anteriores** e confira as mensagens. Inicie outro: a tela deve começar vazia.

Esta entrega precisa do backend atualizado. O app verifica `historicoVersao: 2`; com um servidor antigo, apresenta orientação de atualização em vez de exibir todo o histórico misturado. Não há nova migração, variável de ambiente ou credencial de canal.

## Persistência e isolamento

Os encerramentos já são registrados em `AvaliacaoAtendimento`, com a última entrada do paciente. Esses marcos delimitam cada atendimento da conversa canônica Web da conta. Não há agrupamento por palavras ou exclusão das mensagens para limpar a tela.

Fechamentos legados sem avaliação recebem um marco de encerramento, sem solicitar pesquisa retroativa. A flag antiga `Ativa=false` não faz uma nova entrada posterior ao último encerramento aparecer como encerrada. A persistência de uma interação Web mantém o atendimento ativo; respostas da IA concluídas após encerrar, iniciar outro atendimento ou transferir para a equipe são revalidadas antes de gravar.

Novas mensagens Web/app deixam de aparar automaticamente o histórico em 250 mensagens. As leituras são paginadas. Outros canais mantêm a política anterior. Mensagens já eliminadas por versões antigas não podem ser reconstruídas; nesse caso o app informa a indisponibilidade.

As rotas exigem bearer móvel e usam somente a conversa Web do usuário autenticado. IDs e cursores de outra conta retornam 404. Consultar um arquivo não o reabre e não permite responder nele.

## API móvel

Prefixo `/api/mobile/v1/atendimento`:

| Rota | Conteúdo |
|---|---|
| `GET /historico` | Atendimento atual; `historicoVersao=2`, `cicloId`, `encerrada`, `haAnteriores`, `haMaisMensagens`, avaliação pendente e até 60 mensagens |
| `GET /historico?antesDeMensagemId=...` | Página anterior do mesmo atendimento atual |
| `GET /anteriores` | Até 20 atendimentos finalizados, com assunto, data e `proximoCursor` |
| `GET /anteriores?antesDe=GUID` | Página anterior de atendimentos do titular |
| `GET /anteriores/{id}` | Até 60 mensagens do atendimento selecionado, somente leitura |
| `GET /anteriores/{id}?antesDeMensagemId=...` | Página anterior de mensagens desse arquivo |
| `POST /iniciar` | Abre atendimento vazio; corpo `{"confirmarEncerramento":true}` quando houver conversa atual a finalizar |

As rotas existentes de mensagens, encerramento e avaliação foram preservadas. Polling antigo não pode sobrescrever uma ação mais recente; o app mantém uma única atualização periódica em andamento.

## Validação

As verificações incluem histórico por titular, fronteiras entre atendimentos, conversa vazia após novo início, confirmação antes de arquivar uma conversa ativa, feedback opcional, marcação legada, paginação e preservação acima de 250 mensagens. Há testes nativos para consulta em modo de leitura, ausência de campo de envio no arquivo, início confirmado e texto ampliado. Veja [a validação do Android](../../CallMedAndroid/README-ANDROID.md).
