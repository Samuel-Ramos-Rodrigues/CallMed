# CallMed V50 — APK Release integrado

Este pacote foi montado a partir do projeto completo CallMed V50, substituindo **somente**
o APK distribuído pelo site, os metadados de release e referências documentais/de teste correspondentes.

- Arquivo: `src/CallMed.Web/App_Data/Aplicativo/CallMed.apk`
- Versão verificada no AndroidManifest.xml: **50.0.0**
- versionCode: **5000**
- applicationId: **com.callmed.app**
- minSdk: **26** (Android 8.0)
- Tamanho: **37083797 bytes**
- SHA-256: `47a71fd059483e3d78c8c2901eb696e9b24d0d663e7b28c67d3c1dd14b7d8051`
- O APK possui bloco de assinatura Android APK Signature Scheme v2; **não** foi realizada
  verificação criptográfica completa da assinatura nesta análise.
- A chave privada da assinatura **não foi incluída no ZIP**.

O download anônimo é servido por `/aplicativo/baixar`. Os metadados em `release.json`
correspondem byte a byte ao APK incorporado; a página `/aplicativo` exibe o número de versão.

## Atenção antes da publicação

Esta versão foi assinada com a **nova chave V50**, conforme o processo de geração do proprietário.
Se a instalação anterior foi assinada com chave diferente, será necessário desinstalá-la
ou usar um identificador de pacote alternativo. Desinstalar pode remover dados locais do dispositivo.

**Testes ainda necessários**: compilação .NET com o pacote modificado, abertura de `/aplicativo`,
download real, instalação em dispositivo Android, login, consultas, mensagens e teleconsulta.
O ZIP não contém senhas, arquivos `keystore.properties` nem a chave `.jks`.
