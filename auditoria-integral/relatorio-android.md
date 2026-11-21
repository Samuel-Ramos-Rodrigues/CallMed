# Android Kotlin — análise parcial

- `mobile/android/app/build.gradle`: Kotlin + Jetpack Compose, `applicationId com.callmed.app`, `versionCode 5000`, `versionName 50.0.0`, `minSdk 26`.
- `SessionErrorPolicy.kt`: `isSessionExpired` retorna `true` apenas para 401. Teste Kotlin isolado passou para 401, 403, 404, 409 e 429.
- `src/CallMed.Web/App_Data/Aplicativo/release.json`: tamanho e SHA-256 correspondem ao APK incorporado.
- **Não foi comprovado** que o código-fonte Kotlin atual, incluindo regra 403, foi o código exato utilizado no APK Release. Arquivo de release não contém rastreabilidade de commit.
- `.jks` e senhas não estão presentes no pacote e **não foram solicitados**.

**Pendente:** Gradle build completo, testes JVM instrumentados, decompilação/inspeção do comportamento do APK, atualização assinada com chave local, login, rede, WebRTC e check-in em dispositivos físicos.
