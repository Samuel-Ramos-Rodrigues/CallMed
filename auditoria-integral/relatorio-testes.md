# Testes — resultados executados neste ambiente

| Teste | Resultado | Observação |
|---|---|---|
| ZIP original: `ZipFile.testzip` | APROVADO | 724 arquivos íntegros |
| `node tests/css-integrity.cjs` | APROVADO | contrato CSS, não renderização real |
| `node tests/ux-contract.cjs` | APROVADO | contrato textual/rotas, não browser real |
| `node tests/next/check-transport-state.cjs` na base | **FALSO NEGATIVO** | passou apesar de `CS0103` |
| Novo teste `check-transport-state.cjs` na base sem hotfix | FALHOU COMO ESPERADO | reproduz a falta de variável |
| Novo teste após hotfix | APROVADO | valida padrão de captura no caminho novo |
| `python scripts/check-css.py` | APROVADO | regras CSS estáticas |
| `python scripts/check-architecture.py` | APROVADO | contratos estruturais |
| `python scripts/auditar-next-atual.py` | APROVADO | inventário já existente, não revisão semântica |
| `python scripts/auditar-views-callmed.py` | APROVADO | referências MVC |
| `python scripts/auditar-ux-paginas.py` | APROVADO | scanner heurístico |
| `python scripts/check-v35-classic-visual-contracts.py` | APROVADO | cores e sprites |
| `node --check` em 50 JS/CJS/MJS | 50 APROVADOS | sintaxe; não comportamento |
| `tinycss2` nas 5 CSS | 5 APROVADOS | apenas parse de nível superior |
| `kotlinc` + `SessionErrorPolicySmoke.kt` | APROVADO | apenas política HTTP 401/403 |
| `dotnet restore/build/publish` | NÃO EXECUTADO | SDK .NET 10 indisponível neste ambiente |
| Testes PostgreSQL + EF migrations | NÃO EXECUTADO | banco isolado não provisionado |
| Playwright de sistema autenticado | NÃO EXECUTADO | site .NET não iniciado |
| Gradle Android completo | NÃO EXECUTADO | ambiente Android SDK/assinatura não homologado |
| Provedores externos | NÃO EXECUTADO | credenciais e contas simuladas não conectadas |

A compilação C# é o próximo gate inegociável. Um teste textual positivo não substitui `dotnet build`.
