# TWA do CallMed — associação do novo APK Bubblewrap

## O que foi configurado

O projeto ASP.NET Core recebeu o arquivo publico `CallMedCrud/wwwroot/.well-known/assetlinks.json` com a identidade do APK gerado no Android Studio:

- Dominio web: `https://callmed.onrender.com`
- Identificador do APK Bubblewrap: `com.onrender.callmed.twa`
- SHA-256 do certificado de assinatura obtido do `app-release-signed.apk`:

  `E4:52:22:AE:E6:75:68:DE:91:F2:F2:52:2A:D9:C2:2B:BB:E6:16:72:7C:A0:F0:3A:F1:96:F1:A8:77:5F:44:DA`

**O ZIP ja continha outra TWA**, em `CallMedTWA/`, com o identificador `com.callmed.app`. Sua associacao **nao foi removida**. O endpoint `/.well-known/assetlinks.json` agora junta as declaracoes dinamicas existentes, configuradas no `appsettings.json` ou nas variaveis `Twa__...` do Render, com as declaracoes do arquivo publico do Bubblewrap.

A rota e tratada antes da entrega convencional de arquivos estaticos: isso evita respostas diferentes entre a configuracao do Render e o arquivo versionado. Sem `Twa:Enabled=true`, a rota responde `[]`, como antes.

## Como publicar

1. Publique **o projeto web atualizado** no Render (nao o projeto Android) usando seu processo habitual de deploy.
2. Abra `https://callmed.onrender.com/.well-known/assetlinks.json` no navegador. O retorno precisa ser JSON, sem login nem 404.
3. Confirme que a resposta contem **dois** objetos: um com `com.callmed.app` (TWA antiga) e outro com `com.onrender.callmed.twa` (novo APK), e que o segundo tem a SHA-256 acima.
4. Instale/reabra o `app-release-signed.apk` no Android e verifique se abre sem a barra de endereco do Chrome. A verificacao de Digital Asset Links pode levar algum tempo, e o navegador do aparelho precisa suportar TWA.

## Se for distribuir via Google Play

O APK assinado localmente **nao tem necessariamente** o mesmo certificado que o APK entregue pela Google Play. Para o app `com.onrender.callmed.twa`, adicione no array `sha256_cert_fingerprints` do **arquivo** `CallMedCrud/wwwroot/.well-known/assetlinks.json` a SHA-256 de **App signing key certificate** exibida no Play Console; mantenha a SHA local para testes.

Para o app legado `com.callmed.app`, continue a usar a configuracao `Twa__Sha256CertFingerprints__...` no Render, conforme `docs/deploy/PLAY-STORE-PUBLICACAO.md`.

Nao altere os identificadores dos apps depois de publicados. Guarde o arquivo de chave de assinatura local e as senhas em local seguro; o certificado SHA-256 acima e publico e pode constar do JSON.

## Verificacao local

O arquivo do Bundle/ZIP pode ser testado por validacao de JSON e por inspecao da rota. O build completo .NET e a verificacao da TWA no Android/Render dependem do ambiente de deploy e do aparelho.
