# CallMed — andamento da IA e rascunhos de conversas antigas

## Comportamento

A Central agora separa o modo «Assistente IA» do andamento real da última mensagem recebida. Um painel acima do histórico informa quando a IA está analisando, gerando o rascunho, aguardando aprovação, quando a resposta foi enviada, quando o pedido precisa da equipe ou quando ocorreu falha. A atualização acontece a cada dois segundos enquanto a aba está visível. Perda de conexão na atualização recebe aviso próprio; não é apresentada como falha do modelo.

O progresso é salvo na conversa e continua consultável ao recarregar a página ou abrir outra aba. Processamentos sem confirmação por mais de dez minutos aparecem como interrompidos, permitindo nova tentativa em e-mails pendentes.

O botão **Gerar rascunho** aparece nas conversas de e-mail abertas. Ele usa a última mensagem recebida ainda sem resposta válida, inclusive em conversas antigas ou assumidas pela equipe. Não muda o responsável ou o modo de atendimento humano. A mesma classificação de intenção, proteção de dados e agente existentes continuam sendo usados.

O botão fica desabilitado durante o processamento, quando não há entrada, quando a entrada já foi respondida ou quando já existe rascunho aguardando aprovação. Um rascunho descartado pode ser gerado novamente, mantendo o descartado no histórico. Falhas de transporte usam o reenvio da mensagem existente, para não gerar uma resposta duplicada.

**Devolver para IA** continua transferindo o modo e retomando automaticamente a última entrada pendente. Os dois botões mantêm o histórico visível durante o processamento, com POST autenticado e antiforgery. A alternativa de POST tradicional permanece disponível sem JavaScript.

E-mail continua exigindo **Aprovar e enviar**. Gerar um rascunho nunca envia e-mail. Os canais conversacionais mantêm seu fluxo de envio e agora também registram o progresso da geração.

## Implantação

Publicar o projeto completo no Render. Na inicialização, o patch V26, habilitado por padrão, acrescenta três colunas opcionais em `ConversasAtendimento`: `IaEstado`, `IaEntradaId` e `IaAtualizadoEm`. O patch usa `ADD COLUMN IF NOT EXISTS` e preserva os dados existentes. Se a aplicação usa configuração que desabilita patches, habilitar `Database__ApplyV26Patch=true` para esta versão.

O cache do PWA passou para `callmed-static-v58-progresso-ia-rascunho`, para atualizar o JavaScript e o CSS. Este pacote não publica a aplicação automaticamente.

## Validação desta entrega

- .NET 8: compilação da aplicação, views Razor e projeto de verificações sem erros. Permanecem dois avisos já existentes: nulabilidade em ForgotPassword e função CanalIcon não utilizada.
- 190 verificações de backend aprovadas, incluindo 17 novas verificações: conversa antiga, preservação do atendente, clique repetido, regeneração após descarte, análise/geração automáticas, falhas do classificador/agente, cancelamento, progresso abandonado e mensagem nova durante geração.
- Chromium: sete cenários aprovados com o JavaScript/CSS reais, DOM representativo e endpoints simulados. Cobertura de solicitação assíncrona, andamento, aprovação, falha, reconexão, texto dinâmico e seis combinações de largura/tema (390, 768, 1440 px; claro e escuro), sem rolagem horizontal nem erros JavaScript.
- JavaScript alterado validado com `node --check`; integridade e preservação de arquivos conferidas no ZIP.

Não foram usados Gmail, Gemini ou WhatsApp reais nos testes. O banco PostgreSQL do Render e a tela autenticada em produção não foram acessados; o fluxo completo deve ser conferido após a publicação.
