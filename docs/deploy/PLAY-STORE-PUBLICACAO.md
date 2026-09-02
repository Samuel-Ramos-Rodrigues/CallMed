# CallMed — Publicação na Google Play

Esta versão mantém o projeto Android TWA preparado para gerar um **Android App Bundle (.aab)** assinado e para ser publicado no Google Play.

## Identidade Android

- Nome: **CallMed**
- Package/Application ID: **`com.callmed.app`**
- URL TWA: **`https://callmed.onrender.com`**
- `compileSdk`: **36**
- `targetSdk`: **36 (Android 16)**
- `minSdk`: **26 (Android 8)**
- `versionCode`: **186**
- `versionName`: **18.6**

> O package name é permanente depois que o app é criado no Play Console. Não crie o app no Play Console com outro package se pretende usar esta base.

## 1. Antes de tudo: subir a parte web no Render

Publique a versão atual no Render e confirme:

- `https://callmed.onrender.com`
- `https://callmed.onrender.com/privacidade`
- `https://callmed.onrender.com/.well-known/assetlinks.json`

A política de privacidade já foi adicionada ao projeto. Antes da publicação, configure um e-mail de suporte real no Render:

```env
PlayStore__SupportEmail=seu-email-de-suporte@dominio.com
```

## 2. Criar o app no Play Console

No Google Play Console:

1. **Todos os apps > Criar app**.
2. Nome: `CallMed`.
3. Idioma padrão: `Português (Brasil)`.
4. Tipo: `App`.
5. Gratuito/pago conforme sua decisão.
6. Use um e-mail de contato real.
7. Aceite as declarações e os termos da Assinatura de apps do Google Play.

## 3. Criar a chave de upload

A chave de upload é sua. Ela assina o AAB enviado ao Google Play.

No Windows, dentro de `CallMedTWA`:

```text
CRIAR-CHAVE-UPLOAD-PLAY.bat
```

O script gera:

```text
CallMedTWA/callmed-upload.jks
```

E cria, se necessário:

```text
CallMedTWA/keystore.properties
```

Abra `keystore.properties` e preencha as senhas usadas ao criar a chave.

**Nunca envie estes arquivos ao GitHub:**

- `callmed-upload.jks`
- `keystore.properties`

Eles já estão incluídos no `.gitignore`.

## 4. Gerar o AAB para a Play

Execute:

```text
CallMedTWA/GERAR-AAB-PLAY.bat
```

O arquivo final será:

```text
CallMedTWA/app/build/outputs/bundle/release/app-release.aab
```

É esse arquivo que você envia no Play Console.

## 5. Play App Signing e TWA

O Google Play pode assinar o APK entregue ao usuário com a **App signing key**, diferente da sua chave de upload.

Depois de habilitar o Play App Signing, abra no Play Console a área de **Integridade do app / App signing** e copie a impressão digital **SHA-256 do certificado da chave de assinatura do app (App signing key certificate)**.

No Render, adicione essa SHA-256:

```env
Twa__Sha256CertFingerprints__1=AA:BB:CC:...:FF
```

A versão atual aceita múltiplas impressões digitais. Você pode manter a SHA de debug em `__0` para testes locais e colocar a SHA da Play em `__1`.

Depois de salvar no Render, confira novamente:

```text
https://callmed.onrender.com/.well-known/assetlinks.json
```

O JSON precisa conter `com.callmed.app` e a SHA-256 da **App signing key** da Play. Só assim a versão instalada pela Play abre como TWA confiável sem barra do navegador.

## 6. Informações obrigatórias/relevantes no Play Console

Como o CallMed trata agendamentos e dados relacionados a atendimento de saúde, revise com atenção:

- **Política de Privacidade**: `https://callmed.onrender.com/privacidade`
- **Segurança de dados (Data safety)**
- **Declaração de apps de saúde/medicina**, quando solicitada pelo Play Console
- **Acesso ao app**: se a equipe de revisão precisar fazer login, forneça uma conta de teste funcional no formulário do Play Console
- **Classificação de conteúdo**
- **Público-alvo**
- **Anúncios**: marque de acordo com o comportamento real do app
- **Categoria sugerida**: Medicina / Saúde

O arquivo `PlayStore/DATA-SAFETY-RASCUNHO.md` contém um rascunho baseado nas funcionalidades atuais; confirme tudo antes de enviar.

## 7. Requisito atual de API

A versão atual usa `targetSdk 36`. A partir de **31/08/2026**, novos apps e atualizações de apps móveis enviados ao Google Play precisam segmentar Android 16 / API 36 ou superior.

## 8. Contas pessoais novas: teste fechado

Se sua conta pessoal de desenvolvedor do Google Play foi criada após **13/11/2023**, a Play exige atualmente teste fechado com **pelo menos 12 testadores participando continuamente por 14 dias** antes de pedir acesso à produção.

Você ainda pode usar **Teste interno** antes disso para instalar rapidamente o AAB e conferir o TWA.

## 9. Atualizações futuras

A cada novo AAB, aumente o `versionCode`:

```gradle
versionCode 187
versionName '18.7'
```

Nunca reutilize um `versionCode` já enviado ao Play Console.

## 10. GitHub Actions para AAB

Existe o workflow:

```text
.github/workflows/callmed-play-aab.yml
```

Configure os seguintes secrets no GitHub:

```text
CALLMED_UPLOAD_KEYSTORE_BASE64
CALLMED_KEYSTORE_PASSWORD
CALLMED_KEY_ALIAS
CALLMED_KEY_PASSWORD
```

No PowerShell, para gerar o Base64 da chave:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("CallMedTWA\callmed-upload.jks")) | Set-Clipboard
```

Cole o valor copiado em `CALLMED_UPLOAD_KEYSTORE_BASE64`.

Depois use:

```text
GitHub > Actions > CallMed - AAB Google Play > Run workflow
```

O workflow gera o `.aab` assinado como artifact privado do GitHub Actions.

## 11. Recursos gráficos da ficha da loja

Foi incluído `PlayStore/icon-callmed-512.png` (512×512) como ponto de partida para o ícone da ficha.

Antes de publicar, capture **pelo menos 2 screenshots reais** do aplicativo em celular. Para melhor apresentação, prefira 4 screenshots em 1080×1920 (9:16) mostrando, por exemplo:

1. Início/Visão geral do paciente.
2. Marcar consulta.
3. Central de Confirmações.
4. Minha conta ou lista de espera.

Não use dados pessoais reais de pacientes nas capturas da loja.

O Play Console também trabalha com recurso gráfico 1024×500; crie a peça de loja somente depois que a identidade final estiver fechada para não precisar refazê-la a cada mudança visual.
