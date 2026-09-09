# CallMed V21.6.2 — Parser Evolution resiliente

Correção baseada no erro real observado no Render durante o processamento de `MESSAGES_UPSERT`.

## Correções

- protege o parser estruturado do webhook contra mudanças de formato da Evolution;
- adiciona fallback genérico para extrair `remoteJid`, `senderPn`, `remoteJidAlt`, `sender`, `id`, `fromMe` e texto;
- aceita `message` como objeto, array ou JSON serializado em string;
- amplia suporte a `conversation`, `text`, `caption`, `body`, `contentText`, botões, listas e wrappers;
- mantém compatibilidade com `@lid` e prioriza JID telefônico real;
- impede que payload inesperado gere HTTP 500 apenas durante a extração;
- registra warning quando o parser principal falha e o fallback é utilizado;
- versão Android/TWA atualizada para `21.6.2` (`versionCode 2162`);
- cache PWA atualizado para `callmed-static-v21-6-2-evolution-parser-fix`.

## Observação

O processamento de negócio continua podendo retornar erro se houver falha real no banco, IA ou envio de canal. A mudança desta versão trata especificamente a etapa de interpretação do payload Evolution.
