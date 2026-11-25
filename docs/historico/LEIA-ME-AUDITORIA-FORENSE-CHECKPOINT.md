# CALLMED V54.1 — CHECKPOINT 1 DE AUDITORIA FORENSE

A fonte original tinha 724 arquivos. Este pacote contém a fonte integral, três arquivos corrigidos e os relatórios em `auditoria-integral/`.

**NÃO HOMOLOGADO PARA PRODUÇÃO.** A revisão semântica completa do prompt não foi concluída. Leia `auditoria-integral/progresso-auditoria.md` e `auditoria-integral/relatorio-testes.md`.

## Correções incluídas
1. `AtendimentoOrquestradorService.cs`: corrige CS0103 no caminho de entrada de mensagem IA.
2. `tests/next/check-transport-state.cjs`: reforça teste de regressão; ele falhava na base e passa após correção.
3. `AgendamentoService.cs`: corrige duas mensagens de erro para uso multiprofissional.

Nenhuma migration, classe de banco, provedor, CSS, JS de produção, Kotlin ou APK foi modificado. Sem secrets/keystore.

## Validação local
`node tests/next/check-transport-state.cjs`, `node tests/css-integrity.cjs`, `node tests/ux-contract.cjs`, `python scripts/check-css.py`, `python scripts/check-architecture.py` passaram.

**Antes de publicar:**
```
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release
dotnet publish src/CallMed.Web/CallMed.Web.csproj -c Release --no-restore
```
Depois rode testes Postgres em homologação. Não aplique em produção diretamente.

Relatórios em `auditoria-integral/` contêm cobertura por arquivo, linhas automaticamente varridas e revisão contextual parcial, achados, matriz SENAI, diagramas e guia de implantação.
