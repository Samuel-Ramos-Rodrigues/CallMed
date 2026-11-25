# CallMed NEXT — atualização incremental da **V54.1**

Este projeto é a **V54.1 integral** (V54 + correção Teleconsulta neutra), com correções pontuais de backend/Android e documentação exigida pelo PROMPT MASTER. **Não foi substituído pela V35**. Não é uma reescrita completa nem conclusão de todas as fases do prompt.

## Acesso à entrega

- `docs/next-generation/V54_1/01-AUDITORIA-E-MATRIZ.md` — riscos, classificação por funcionalidade, priorização.
- `02-ARQUITETURA-E-DIAGRAMAS.md` — monólito modular e oito diagramas conceituais.
- `03-INDICADORES-E-CENARIOS-SENAI.md` — métricas definidas e demonstração com dados sintéticos.
- `04-TESTES-IMPLANTACAO-E-PRIVACIDADE.md` — testes executados, não executados, deploy e fontes regulatórias.
- `05-CHANGELOG-E-PENDENCIAS.md` — arquivos modificados e pontos que ainda faltam.
- `INVENTARIO-ESTATICO.json` — inventário gerado por script, não comprovação de operação real.

## Validação inicial

```bash
node tests/css-integrity.cjs
node tests/ux-contract.cjs
node tests/next/check-transport-state.cjs
python scripts/check-css.py
python scripts/check-v35-classic-visual-contracts.py
python scripts/check-architecture.py
python scripts/auditar-views-callmed.py
python scripts/auditar-ux-paginas.py
python scripts/auditar-next-atual.py
```

Para backend/Android e PostgreSQL, consulte `04-TESTES-IMPLANTACAO-E-PRIVACIDADE.md` e **não rode suites que mutam dados na produção**. Não inclua senhas nem keystore no Git.

**Nota importante:** o APK release V50 distribuído pelo site foi preservado sem alteração. A correção Kotlin da sessão HTTP 403 ainda **não está compilada no APK**. Recompile, execute os testes e assine uma nova versão com chave privada local antes da distribuição.

**Base GitHub:** as atualizações V54/V54.1 não haviam sido mergeadas na `master` do GitHub no momento desta entrega. Este ZIP é uma cópia da base V54.1 fornecida no histórico, modificada localmente; nenhuma alteração remota ou deploy foi realizado.
