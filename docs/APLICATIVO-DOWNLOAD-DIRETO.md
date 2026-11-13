# APK nativo disponível no próprio site

O download entrega o aplicativo nativo de pacientes **CallMed 23.2.0**, criado em Kotlin e Jetpack Compose, com os novos fluxos e ajustes de leitura. O módulo está em [`CallMedAndroid`](../CallMedAndroid/README-ANDROID.md). O projeto TWA anterior foi preservado em sua pasta.

## Publicar no Render

1. Atualize o repositório com este projeto completo e publique o serviço existente. Esta entrega inclui as rotas `/api/mobile/v1` usadas pelo nativo.
2. Inclua `CallMedCrud/App_Data/Aplicativo/CallMed.apk` e `release.json`. O `.gitignore` libera especificamente esse APK público; as chaves privadas continuam ignoradas.
3. O `.csproj` copia os dois arquivos para a saída de publicação. O Dockerfile os leva ao contêiner. As pastas de código Android e as chaves de assinatura ficam fora do contexto Docker.
4. Após publicar, abra `https://callmed.onrender.com/aplicativo` e confira a versão **23.2.0**.
5. Baixe o APK, abra-o no celular e confirme a instalação. Se necessário, autorize a instalação pelo navegador usado no download.

Publique o servidor antes de entrar no novo app. Não foi criada variável de ambiente nem migração adicional para essa integração. As migrações já existentes, incluindo chaves Data Protection, perfis e sessões, continuam necessárias.

O arquivo fica junto do código publicado, sem depender de upload para o disco temporário do Render. Cada publicação deve incluir os dois arquivos do diretório `App_Data/Aplicativo`.

## Pacote conferido

| Campo | Valor |
|---|---|
| Pacote | `com.callmed.app` |
| Versão | `23.2.0` |
| Código | `2320` |
| Android mínimo | 8.0 / API 26 |
| Tamanho | 1.457.994 bytes |
| Activity inicial | `com.callmed.app.MainActivity` |
| Assinatura | APK v2 verificada; mesma chave de produção anterior |
| Debug | Desabilitado no release |
| SHA-256 | `ec71f93714b1862cb143a8c14da14a4a0c472f960f772ed109787006af149a1f` |

O SHA-256 do APK identifica o conteúdo do arquivo; a impressão digital do certificado identifica a assinatura. O código 2320 é superior ao nativo 2300 e ao TWA 2220. A mesma chave permite atualizar a instalação direta anterior sem desinstalar. Instalações de debug ou assinadas por outra chave exigem conferir a compatibilidade de assinatura.

## Download

A página pública `/aplicativo` mostra versão, tamanho, compatibilidade e instruções. O botão da página inicial e os links nos rodapés apontam para ela. A rota `/aplicativo/baixar` serve somente o APK fixo, com MIME Android, nome `CallMed.apk`, proteção contra cache antigo e suporte a retomada HTTP Range.

A pasta `App_Data` não é exposta pelo middleware estático. Parâmetros não permitem selecionar outros arquivos. Se o pacote ou os metadados estiverem ausentes/inválidos, a página mostra indisponibilidade e o download retorna 404.

A instalação PWA do site continua disponível. As telas do novo APK são nativas; criação de conta e recuperação de senha abrem seus fluxos existentes no navegador.

## Atualizar o app

Aumente o `versionCode` no módulo `CallMedAndroid`, mantenha a chave e gere `assembleRelease`. Substitua `CallMed.apk` e atualize `release.json` com a versão, código, tamanho, SHA-256 e Android mínimo reais. Atualize também as expectativas de versão/hash dos testes de download. Publique os dois arquivos juntos.

O aplicativo não instala atualizações automaticamente. Mudanças no cliente Kotlin exigem novo APK; mudanças compatíveis nas APIs são publicadas no servidor.

## Verificação

A compilação e publicação Release do servidor incluem o mesmo APK. Os 13 testes HTTP de download e as 12 verificações da interface no Chromium passaram com este arquivo. A compilação Android release, o lint e os 35 testes JVM/Compose também passaram. Veja [a validação completa](../CallMedAndroid/README-ANDROID.md).
