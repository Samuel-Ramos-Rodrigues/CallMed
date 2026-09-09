# CallMed V21.6.1 — Evolution / WhatsApp

Correções de compatibilidade e diagnóstico da integração Evolution API.

## Alterações

- Webhook Evolution agora trata `remoteJid` em formato `@lid` e prioriza `senderPn`/`remoteJidAlt` quando houver número telefônico real.
- Extração de texto ampliada para mensagens simples, extended text, mídia com legenda, botões/listas e wrappers `ephemeral`/`viewOnce`.
- Logs estruturados de entrada, processamento e falha do webhook sem registrar o conteúdo da mensagem ou o segredo.
- Envio Evolution mantém o payload atual e permite fallback legado em 400/422 e em 500 apenas quando a resposta indica incompatibilidade de payload.
- Resposta de erro da Evolution é limitada no log para facilitar diagnóstico.
- `DbUpdateException` de mensagem externa só é ignorada quando for realmente a violação do índice único de idempotência.
- Paciente resolvido por telefone/e-mail é anexado ao EF como entidade existente antes da criação da conversa, evitando tentativa de INSERT duplicado no webhook.
- Nome padrão da instância alterado de `mksan` para `callmed`.
- TWA atualizado para `versionCode 2161` / `versionName 21.6.1`.
- Nomes dos artifacts do GitHub Actions atualizados para V21.6.1.
- Cache-busting de assets atualizado para `2161` e Service Worker para `callmed-static-v21-6-1-evolution-fix`.

## Observação

O nome configurado em `Atendimento__WhatsApp__Evolution__InstanceName` precisa corresponder ao nome real da instância criada na Evolution API. A variável de ambiente do Render continua tendo precedência sobre o valor padrão do `appsettings.json`.
