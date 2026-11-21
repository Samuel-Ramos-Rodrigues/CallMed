# CallMed V52 — coerência visual

O CallMed V52 é uma correção de regressões sobre a V51 completa: ícones SVG, contraste claro/escuro da landing, grid das etapas, rodapé de login, atalhos do paciente e cache do PWA.

- `docs/ui/AUDITORIA-REGRESSOES-V52.md`: diagnóstico e limites.
- `scripts/check-v52-visual-contracts.py`: teste de regressões que pode ser executado localmente.
- APK Release Android V50, banco, controllers, migrations e integrações: **preservados**.

## Local

```powershell
node tests/css-integrity.cjs
node tests/ux-contract.cjs
python scripts/check-css.py
python scripts/check-v52-visual-contracts.py
dotnet build CallMed.sln -c Release
```

Verifique a página pública com zoom 100% (`Ctrl+0`), tema claro e escuro, depois os quatro perfis com contas de homologação. A implantação no Render **não foi feita**.
