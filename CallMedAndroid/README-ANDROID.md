# CallMed Android nativo

Aplicativo de pacientes em Kotlin e Jetpack Compose, versão **34.0.0**, código **3400**, Android mínimo **8.0 / API 26**. Usa o mesmo servidor e banco do site por HTTPS e pela API `/api/mobile/v1`.

A reorganização V35 do backend mantém os contratos do aplicativo. O APK entregue em `../CallMedCrud/App_Data/Aplicativo/CallMed.apk` e seu arquivo `release.json` foram preservados. O site distribui o instalador em `/aplicativo`.

## Desenvolvimento

Abra esta pasta no Android Studio, sincronize o Gradle e use JDK 17 ou compatível e SDK Android 36. Para publicar atualizações, restaure LOCALMENTE a chave de assinatura utilizada nas versões anteriores e configure `keystore.properties` fora do ZIP distribuído (veja `keystore.properties.example`). Não substitua a chave original sem avaliar o processo de atualização ou reset da chave de upload com a loja. Mantenha ambos os arquivos privados; **eles foram excluídos desta distribuição por segurança**.

```sh
./gradlew testDebugUnitTest lintRelease assembleRelease
```

No Windows, use `GERAR-APK-NATIVO.bat`; no Linux, `./GERAR-APK-NATIVO.sh`. A assinatura também aceita `CALLMED_KEYSTORE_FILE`, `CALLMED_KEYSTORE_PASSWORD`, `CALLMED_KEY_ALIAS` e `CALLMED_KEY_PASSWORD`.

## Responsabilidades

- `CallMedViewModel`: estado e ações da interface.
- `CallMedApi`: comunicação HTTPS e renovação de tokens.
- `SessionStore` e `ClinicalCrypto`: proteção das credenciais e chaves locais.
- `BookingScreens`, `JourneyScreens`, `AccountScreens` e `ChatScreen`: fluxos do paciente.
- `NativeTele` e `CallAudio`: WebRTC e controle da mídia.

O app oferece consultas, agendamento, acompanhamento, preparação protegida, chat, conta e chamada nativa. A gestão da clínica permanece no site. Os avisos por WhatsApp, e-mail e httpSMS são processados no servidor. Não há operação clínica offline nem push Android próprio.

A verificação em aparelhos físicos continua necessária para TalkBack, permissões, áudio, câmera e redes distintas. Veja a arquitetura e implantação nos documentos da raiz do projeto.
