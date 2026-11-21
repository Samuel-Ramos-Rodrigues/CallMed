# Guia de implantação — após homologação

1. **Preservar a versão publicada**. Fazer backup verificável do banco e configurar ambiente de homologação com PostgreSQL isolado. Não restaurar banco real em dados sintéticos.
2. **Conferir alterações**: executar `python auditoria-integral/verificar-cobertura.py` e revisar `inventario-pos-correcao.json` (3 arquivos-fonte/teste modificados). As demais diferenças são somente relatórios adicionados.
3. **Compilar com .NET 10** (obrigatório, não executado neste ambiente):

```bash
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release
dotnet publish src/CallMed.Web/CallMed.Web.csproj -c Release -o ./artifacts/web
```

4. **Executar testes com dados sintéticos e PostgreSQL** (somente no ambiente de homologação): `dotnet tests/CallMed.Checks/bin/Release/net10.0/CallMed.Checks.dll`; repetir suites de postgres e autorizacao conforme workflow `.github/workflows/callmed-quality.yml`.
5. **Verificar webhooks/worker sem credenciais reais no repositório**. `Jobs:Mode` no Web e Worker precisa corresponder ao modo escolhido; não iniciar dois executores em paralelo sem isolamento.
6. **Executar smoke tests autenticados**: os quatro perfis e as jornadas SENAI, com dados totalmente sintéticos.
7. **Só depois fazer merge e deploy controlado no Render**. Monitorar status do build, logs, health checks e filas. Manter rollback do deploy anterior.
8. **Android**: APK distribuído permanece V50. Para incluir novos códigos Kotlin, compilar e assinar no equipamento responsável, usando chave fora do repositório. Não transmitir keystore/senhas.

Este checkpoint não exige novas variáveis nem migrations. Não altera os artefatos de produção automaticamente.
