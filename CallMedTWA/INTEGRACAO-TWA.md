# Integração do TWA — 05/11/2026

Base: última entrega com perfil e avaliação direta. Dela foram preservados o site, os canais, a recuperação de senha, o desempenho do chat, o perfil, as preferências e o patch V30.

Do ZIP enviado pelo usuário foram incorporados somente os novos arquivos do TWA:

- `CallMedTWA/callmed-upload.jks`
- `CallMedTWA/keystore.properties`
- `CallMedTWA/gradle/wrapper/gradle-wrapper.jar`

A associação do site foi atualizada em `CallMedCrud/appsettings.json` (somente `Twa.Sha256CertFingerprints`) e `CallMedCrud/wwwroot/.well-known/assetlinks.json`, com o pacote `com.callmed.app`. A assinatura de produção corresponde à chave enviada. A assinatura debug anterior também foi mantida para compatibilidade com o APK de teste.

Os demais 35 arquivos do módulo Android já eram iguais nas duas versões e foram mantidos. Configurações de IDE, caches Gradle e relatórios de build da máquina de origem não foram importados.

## Utilização

Publique o site atualizado no Render para disponibilizar a associação do domínio. Para gerar o aplicativo, abra a pasta `CallMedTWA` no Android Studio ou utilize os scripts já existentes. O módulo usa `applicationId` `com.callmed.app` e a versão efetiva definida em `app/build.gradle` é `22.2.0` (`versionCode` `2220`).

As chaves e o arquivo de credenciais fornecidos estão neste ZIP para seu uso local. Mantenha-os em armazenamento privado; as regras existentes do `.gitignore` continuam impedindo seu versionamento. Não envie esses arquivos ao repositório público.

## Validação desta integração

- Chave de produção e senha da chave conferidas pelo Java KeyStore.
- Chave debug e credenciais padrão conferidas pelo Java KeyStore.
- Certificado de produção corresponde ao assetlinks enviado.
- appsettings preserva integralmente os valores fora da associação TWA.
- Pacote Android igual no appsettings e no assetlinks.
- Assinaturas debug e release reconhecidas nas duas fontes da associação.
- URL inicial do Android aponta para a origem configurada do CallMed.
- Declaração Android aponta para o mesmo domínio do site.
- Autoridade do FileProvider corresponde ao pacote Android.
- Manifesto e recursos XML do Android válidos.
- Gradle Wrapper JAR íntegro e com a classe principal esperada.
- CallMedTWA/callmed-upload.jks: conteúdo preservado exatamente como recebido.
- CallMedTWA/keystore.properties: conteúdo preservado exatamente como recebido.
- CallMedTWA/gradle/wrapper/gradle-wrapper.jar: conteúdo preservado exatamente como recebido.
- PWA preservado com cache v68 e script do novo perfil.
- Só os dois arquivos de associação TWA diferem entre os arquivos existentes.
- Todos os arquivos C# da versão mais recente preservados byte por byte.
- Proteção de versionamento das chaves de produção preservada no .gitignore.

Não houve alteração de código C# ou de dependências do site. Nesta integração não foi gerado nem instalado um APK/AAB: isso exige o Android SDK e o teste no dispositivo. Os resultados antigos de testes do site continuam incluídos como histórico da entrega anterior.
