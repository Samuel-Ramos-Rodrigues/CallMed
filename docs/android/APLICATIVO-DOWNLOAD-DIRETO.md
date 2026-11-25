# Distribuir o aplicativo Android pelo site

**Migração da versão 50 para uma nova assinatura.** Abra ASSINAR-CALLMED.bat na raiz do projeto. Consulte o [passo a passo para criar e guardar a chave](../../mobile/android/NOVA-ASSINATURA.md). As instruções de atualização convencional abaixo exigem a chave da versão já instalada.


A página pública `/aplicativo` lê a versão, o tamanho e o Android mínimo de `src/CallMed.Web/App_Data/Aplicativo/release.json`. O botão baixa o arquivo `CallMed.apk` da mesma pasta pela rota `/aplicativo/baixar`.

## Preparar uma atualização

1. Na máquina que guarda a chave da versão instalada, abra o projeto `mobile/android` e configure a assinatura conforme o guia Android.
2. Para assinar o candidato 54.1.4 já compilado, execute [ASSINAR-APK-PRONTO.bat](../../mobile/android/ASSINAR-APK-PRONTO.bat). Se alterou o código Kotlin, compile e assine com [GERAR-APK-NATIVO.bat](../../mobile/android/GERAR-APK-NATIVO.bat) no Windows ou [GERAR-APK-NATIVO.sh](../../mobile/android/GERAR-APK-NATIVO.sh) no Linux. A tarefa de preparação compara assinatura, pacote e versão, e calcula o hash do instalador.
3. Revise e inclua juntos `CallMed.apk` e `release.json` no próximo deploy. A publicação do projeto web já copia esses arquivos para o servidor.
4. Abra `/aplicativo`, confira a versão anunciada e faça a atualização em um aparelho que já tenha a versão anterior. Abra o aplicativo e verifique se a sessão e as consultas continuam acessíveis.

[Configurar assinatura e compilar o aplicativo](../../mobile/android/README-ANDROID.md)
