# CallMed Android / TWA

Wrapper Android da PWA CallMed usando Trusted Web Activity.

## Versão

- `versionCode`: 2130
- `versionName`: `21.3.0`
- `applicationId`: `com.callmed.app`
- `minSdk`: 26
- `targetSdk`: 36

## APK de teste

No Windows, execute `GERAR-APK-DEBUG.bat` ou use o workflow `CallMed TWA - APK Debug` no GitHub Actions.

## AAB da Google Play

Use `GERAR-AAB-PLAY.bat` / `GERAR-AAB-PLAY.sh` com `keystore.properties`, ou o workflow `CallMed - AAB Google Play` com os secrets de assinatura configurados.

Nunca envie a chave de produção para o repositório. O arquivo `debug.keystore` incluído é somente para builds de teste e possui credenciais padrão de debug.
