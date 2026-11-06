# APK disponível no próprio site

Entrega de 05/11/2026, sobre a última versão com perfil, avaliações e TWA integrado.

## O que foi incluído

- APK de produção recebido do usuário: CallMed 22.2.0, código 2220, Android 8.0 (API 26) ou superior.
- Página pública `/aplicativo`, com versão, tamanho, botão de download e instruções de instalação.
- Download público em `/aplicativo/baixar`, sem exigir login.
- Botão “Baixar para Android” na página inicial pública e link “Aplicativo Android” nos rodapés público, autenticado e de login.
- Instalação do PWA pela tela inicial preservada como opção separada.

O TWA continua sendo a estrutura do aplicativo Android. A distribuição agora pode ser feita pelo próprio site, com o APK assinado, sem publicar na Play Store.

## Publicar no Render

1. Atualize o repositório com o conteúdo completo deste projeto e publique novamente o serviço existente.
2. Inclua `CallMedCrud/App_Data/Aplicativo/CallMed.apk` e `release.json`. O `.gitignore` já libera especificamente esse APK público; as chaves privadas continuam ignoradas.
3. O `.csproj` copia o APK e os metadados na saída de publicação. O Dockerfile existente leva os dois arquivos ao contêiner.
4. Após a publicação, acesse `https://callmed.onrender.com/aplicativo` e faça o download pelo botão.
5. Abra o arquivo no Android e confirme a instalação. O sistema pode solicitar permissão para instalar pelo navegador utilizado.

Esta mudança não exige migração do banco, nova variável ou alteração dos provedores de canais. Os patches e as configurações anteriores continuam como estavam.

O arquivo fica junto do código publicado, sem depender de upload para o disco temporário do Render. Cada nova publicação deve continuar incluindo os dois arquivos do diretório `App_Data/Aplicativo`.

## Arquivo e assinatura conferidos

- Pacote: `com.callmed.app`
- Versão: `22.2.0`
- Código da versão: `2220`
- Tamanho: `3814239` bytes
- Build de produção, com `debuggable=false`.
- Assinatura APK v2 validada pelo verificador Android `apksig` 8.13.2.
- Certificado coincide com a chave de produção e com o `assetlinks.json` do projeto.
- SHA-256 do arquivo APK:

```text
814ab383ac93eb1e9746ea01593f850305c3166901f2cca257df53e2e00f28fb
```

O SHA-256 do arquivo é diferente da impressão digital do certificado. O primeiro identifica o conteúdo deste APK; o segundo associa o aplicativo assinado ao domínio do site.

## Comportamento do download

A rota serve somente o APK fixo, com MIME `application/vnd.android.package-archive`, nome `CallMed.apk` e suporte a HTTP Range para retomada. Não aceita parâmetros para escolher outros arquivos. O APK não é armazenado no cache do service worker.

A pasta `App_Data` não é publicada como uma área de arquivos estáticos. O controlador disponibiliza apenas o APK. As chaves e o arquivo de credenciais do TWA não são servidos pelo site.

Se o APK ou os metadados estiverem ausentes/inválidos, a página apresenta um aviso e o download retorna 404. Ela não oferece um botão que aponte para um pacote ausente.

## Próximas versões

Mantenha a mesma chave de assinatura e aumente o `versionCode` no módulo Android. Gere outro APK com `assembleRelease`, substitua `CallMed.apk` e atualize `release.json` com a versão, o código, o tamanho em bytes, o SHA-256 e o Android mínimo correspondentes. Depois, publique novamente o site.

O download não atualiza o aplicativo automaticamente: o usuário baixa e instala a nova versão. As alterações das páginas e serviços web continuam chegando pelo site normalmente.

## Testes

```bash
dotnet run --project tests/CallMed.Checks
# Somente download, página e respostas HTTP:
CALLMED_APK_FIXTURES=/tmp/callmed-apk dotnet run --project tests/CallMed.Checks -- --apk
# Interface com HTML renderizado pelo teste HTTP e CSS/JS reais:
CALLMED_APK_FIXTURES=/tmp/callmed-apk NODE_PATH=<node_modules> CHROME_PATH=<chromium> node tests/aplicativo-browser.cjs
```

Validação realizada: compilação e publicação Release, 452 verificações do servidor, 12 verificações da interface no Chromium e conferência de que o APK publicado mantém exatamente o hash do recebido. Nenhuma mensagem real foi enviada. A instalação em aparelho e a publicação no Render ficam para a homologação após atualizar o serviço.
