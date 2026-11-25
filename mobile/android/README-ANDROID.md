# CallMed Android nativo

**Migração da versão 50 para uma nova assinatura.** Abra ASSINAR-CALLMED.bat na raiz do projeto. Consulte o [passo a passo para criar e guardar a chave](NOVA-ASSINATURA.md). As instruções de atualização convencional abaixo exigem a chave da versão já instalada.


Aplicativo de pacientes em Kotlin e Jetpack Compose, versão do código **54.1.4**, código **5414**, Android mínimo **8.0 / API 26**. Usa o mesmo servidor e banco do site por HTTPS e pela API `/api/mobile/v1`.

O manifesto ao lado do instalador informa a versão efetivamente disponível. O APK Release 50.0.0 está incluído em `../../src/CallMed.Web/App_Data/Aplicativo/CallMed.apk`, com metadados atualizados no `release.json`. A chave de assinatura não é distribuída. O site distribui o instalador em `/aplicativo`.

## Atualizar o aplicativo do site

No Windows, abra a pasta `mobile/android` e execute **ASSINAR-APK-PRONTO.bat**. O pacote inclui o candidato de produção `precompilado/CallMed-54.1.4-sem-assinatura.apk`. O script usa o Android SDK e a configuração de assinatura da clínica (`keystore.properties` ou as variáveis indicadas abaixo), verifica o identificador, a versão e o certificado da edição anterior e prepara estes arquivos para o mesmo deploy:

- `src/CallMed.Web/App_Data/Aplicativo/CallMed.apk`
- `src/CallMed.Web/App_Data/Aplicativo/release.json`

No Linux, o equivalente é `sh gradlew :app:prepararApkSite -PcallmedUsarPrecompilado`. Para recompilar a partir de alterações locais, use **GERAR-APK-NATIVO.bat**. Os dados da assinatura ficam no computador que prepara o pacote. O Render recebe o APK já assinado e seu manifesto.

O instalador **CallMed Teste 54.1.4** usa o identificador `com.callmed.app.teste`, tem as otimizações de produção e pode ser instalado ao lado da versão do site. A conta e as permissões dessa instalação são independentes. Para compilá-lo use `sh gradlew :app:assembleHomologacao`.

## Diagnóstico da videochamada

O APK 50.0.0 incluído no projeto ativava a tela de vídeo antes de receber a configuração da sala. Sua inicialização do renderizador ficava fora do tratamento da entrada. A versão 54.1.4 aguarda o primeiro quadro da câmera, usa a interface de câmera compatível e mantém visível a falha de entrada enquanto a situação da consulta é atualizada. A opção **Abrir consulta no navegador** fica disponível se a câmera ou o vídeo falhar.

Se o sistema operacional registrar uma falha do processo durante a chamada, no Android 11 ou superior a próxima visita à sala pode exibir `VIDEO-NATIVO`, `VIDEO-EXCECAO` ou `VIDEO-SEM-RESPOSTA`. Esse diagnóstico usa uma marca local da operação. O código identifica o tipo de encerramento e dispensa o envio de dados de pacientes. Para localizar a instrução exata em um aparelho específico é preciso o trecho da pilha da falha do Android.

## Desenvolvimento

Abra esta pasta no Android Studio, sincronize o Gradle e use JDK 17 ou compatível e SDK Android 36. Para publicar atualizações, restaure LOCALMENTE a chave de assinatura utilizada nas versões anteriores e configure `keystore.properties` fora do ZIP distribuído (veja `keystore.properties.example`). Não substitua a chave original sem avaliar o processo de atualização ou reset da chave de upload com a loja. Mantenha ambos os arquivos privados; **eles foram excluídos desta distribuição por segurança**.

```sh
./gradlew testDebugUnitTest lintRelease assembleRelease
```

Os scripts conferem a assinatura, a versão e o hash, depois preparam o APK e o manifesto na pasta de download do site. Publique os dois juntos no próximo deploy. No Windows, use `GERAR-APK-NATIVO.bat`; no Linux, `./GERAR-APK-NATIVO.sh`. A assinatura também aceita `CALLMED_KEYSTORE_FILE`, `CALLMED_KEYSTORE_PASSWORD`, `CALLMED_KEY_ALIAS` e `CALLMED_KEY_PASSWORD`.

## Responsabilidades

- `CallMedViewModel`: estado e ações da interface.
- `CallMedApi`: comunicação HTTPS e renovação de tokens.
- `SessionStore` e `ClinicalCrypto`: proteção das credenciais e chaves locais.
- `BookingScreens`, `JourneyScreens`, `AccountScreens` e `ChatScreen`: fluxos do paciente.
- `NativeTele` e `CallAudio`: WebRTC e controle da mídia.

O app oferece consultas, agendamento, acompanhamento, preparação protegida, chat, conta e chamada nativa. A gestão da clínica permanece no site. Os avisos por WhatsApp, e-mail e httpSMS são processados no servidor. Não há operação clínica offline nem push Android próprio.

A verificação em aparelhos físicos continua necessária para TalkBack, permissões, áudio, câmera e redes distintas. Veja a arquitetura e implantação nos documentos da raiz do projeto.

## Testar a entrada na sala

`sh gradlew :app:testDebugUnitTest` cobre, entre outros casos, permissão revogada, falha da biblioteca nativa, câmera que não envia o primeiro quadro, toques repetidos e tentativas sobrepostas. Para exercitar o WebRTC do Android em um aparelho ou emulador com câmera, execute `sh gradlew :app:connectedDebugAndroidTest`. Esse teste abre a câmera, cria as superfícies de vídeo, sai e entra de novo com sinalização local simulada. A chamada entre paciente e profissional em redes reais continua sendo uma validação de ponta a ponta separada.

Na entrega 54.1.4, passaram 93 testes locais. O teste instrumentado também passou em um emulador Android 11 x86_64 com câmera emulada. Ele abriu a câmera, criou as duas superfícies, encerrou a chamada e repetiu a entrada. A conferência de assinatura rejeitou uma chave divergente e preparou o APK e o manifesto com uma chave correspondente em uma cópia de teste.
