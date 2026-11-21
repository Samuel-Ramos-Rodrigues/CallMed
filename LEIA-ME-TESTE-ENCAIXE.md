# Teste de encaixe automático — CallMed NEXT V54.1

**Esta entrega implementa um teste de integração com PostgreSQL real** para cancelar uma consulta, gerar a oferta, criar tarefa de contato, aceitar, disputar concorrencialmente, recusar reuso e expirar uma oferta. O sistema de produção e o APK não foram alterados.

## Execução

Leia obrigatoriamente `docs/next-generation/testes/ENCAIXE-PG-TESTE-PONTA-A-PONTA.md` antes de executar. **O teste apaga o banco local `callmed_test_encaixe`** e não pode ser usado contra produção. Crie esse banco vazio usando pgAdmin ou psql e configure a conexão local conforme a documentação.

```powershell
$env:CALLMED_TEST_DB = 'Host=127.0.0.1;Port=5432;Database=callmed_test_encaixe;Username=postgres;Password=SENHA_LOCAL'
$env:CALLMED_TEST_RESET = 'SIM'
dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj -c Release -- --encaixe-pg
```

**Resultado esperado:** verificações `PASS ENCAIXE-PG` e, ao final, `ENCAIXE-PG: execução completa`. A execução real está pendente neste ambiente porque não há SDK .NET nem servidor PostgreSQL local. Não trate o teste como aprovado sem esse resultado.

### Arquivos alterados/adicionados

- `tests/CallMed.Checks/EncaixeCancelamentoPostgresChecks.cs`: cenário de integração com dois contextos PostgreSQL concorrentes
- `tests/CallMed.Checks/Program.cs`: opção `--encaixe-pg`
- `.github/workflows/callmed-quality.yml`: job de CI exclusivo com banco PostgreSQL efêmero
- `global.json`: mínimo ajustado para SDK 10.0.401, com `latestPatch` (compatível com 10.0.404)
- `docs/next-generation/testes/ENCAIXE-PG-TESTE-PONTA-A-PONTA.md`: passos, resultados e limitações

Não há mudanças de controllers, serviços clínicos, banco/migrations, provedores externos, UI, código Android ou APK.
