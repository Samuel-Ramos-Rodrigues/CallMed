# Comparativo antes/depois — correções verificáveis

| Arquivo | Antes | Depois | Evidência de teste |
|---|---|---|---|
| `AtendimentoOrquestradorService.cs`, linha 473 | resultado do envio descartado, `saida` não definido na linha 474 | mensagem capturada em `var saida` antes de consultar `.Status` | novo teste falhou antes / passou depois; log externo Render comprovou CS0103 |
| `tests/next/check-transport-state.cjs` | verificava 3 ocorrências textuais de `Persistir` e não detectava variável inexistente | exige captura da saída no caminho de mensagem nova | `AssertionError` antes / sucesso após correção |
| `AgendamentoService.cs`, linhas 280 e 412 | mensagem “Psicólogo inválido...” também para outras profissões | mensagem genérica “Profissional inválido...” | comparação textual; fluxo de UI ainda não executado |

**Preservado sem alteração:** banco, migrations, CSS, JS de produção, provedores, configurações, Kotlin e APK Release. Nenhum arquivo removido.
