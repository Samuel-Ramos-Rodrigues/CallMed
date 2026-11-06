# CallMed Android nativo — 23.0.0

Aplicativo de pacientes em Kotlin e Jetpack Compose. As telas são componentes Android; não há WebView, Custom Tab de navegação nem dependência do TWA. O site ASP.NET Core continua sendo o servidor e usa o mesmo banco.

## Primeiro uso

1. Publique a pasta `CallMedCrud` desta entrega no Render. A API nova fica em `https://callmed.onrender.com/api/mobile/v1`.
2. Instale o APK disponibilizado em `/aplicativo` no site atualizado. Ele também está em `CallMedCrud/App_Data/Aplicativo/CallMed.apk` neste projeto.
3. Entre com CPF ou e-mail e a senha de uma conta **Paciente** já cadastrada. Contas da equipe e contas com autenticação em dois fatores não entram nesta primeira versão móvel.

O servidor precisa desta atualização antes do primeiro login nativo. Nenhuma variável nova de ambiente ou migração adicional foi criada para o app; as migrações já existentes, incluindo as tabelas de chaves e sessões, continuam necessárias.

## Funções disponíveis

- Login e restauração de sessão, saída e troca de senha.
- Início com próxima consulta e atalhos.
- Consultas próprias: histórico, confirmação, remarcação e cancelamento, conforme situação e prazo definidos no servidor.
- Agendamento por médico, datas e vagas reais; resumo e confirmação antes de gravar; atendimento particular ou convênio conforme cadastro e regras da clínica.
- Perfil: foto JPG/PNG até 2 MB, nome, telefone, nascimento, convênio, tema e preferências de contato. CPF e e-mail ficam em leitura nesta versão.
- Chat com o mesmo histórico do site, pausa da IA durante atendimento humano, encerramento e avaliação opcional.

**Criar conta** e **Esqueci minha senha** abrem o navegador com os fluxos existentes do site. O app não sincroniza cookies com o navegador. Não inclui ainda exames, gestão da equipe, notificações push próprias, cadastro nativo ou ações offline. As preferências de SMS/e-mail/WhatsApp continuam controlando os avisos já disponíveis no servidor; não ativam push Android.

## Abrir e compilar

Abra **esta pasta `CallMedAndroid`**, e não `CallMedTWA`, no Android Studio. Use JDK 17, Android SDK 36 e permita a sincronização do Gradle. O wrapper baixa Gradle 8.13. Dependências estão fixadas no projeto.

No Windows, execute `GERAR-APK-NATIVO.bat`, ou no terminal desta pasta:

```powershell
.\gradlew.bat assembleRelease
```

No Linux/macOS:

```sh
./gradlew assembleRelease
```

Saída: `app/build/outputs/apk/release/app-release.apk`.

Para testes JVM: `./gradlew testReleaseUnitTest`. Para análise Android: `./gradlew lintRelease`.

O release usa R8 para reduzir código e recursos. O mapa de ofuscação deste APK está em `release/mapping-23.0.0.txt.gz`; descompacte-o para analisar um relatório de falha desta versão.

O APK release usa `com.callmed.app`, código **2300**, versão **23.0.0**, Android 8.0 ou superior. A assinatura usa a mesma chave de upload que já acompanhava o projeto. O código de versão é superior ao APK TWA anterior; assim, a instalação direta pode atualizar o APK antigo assinado com essa chave. Uma instalação distribuída pela Play Store com outra chave de assinatura exige verificar o certificado correspondente.

`keystore.properties` aponta para a chave local. Variáveis opcionais para CI: `CALLMED_KEYSTORE_FILE`, `CALLMED_KEYSTORE_PASSWORD`, `CALLMED_KEY_ALIAS`, `CALLMED_KEY_PASSWORD`. Não coloque esses arquivos nem senhas em repositórios públicos. O Render não precisa da chave para servir o APK já assinado; o contexto Docker exclui ambas as pastas Android.

O host está em `app/build.gradle` (`BuildConfig.API_BASE_URL`). Se mudar de domínio, atualize esse valor e gere outro APK. Credenciais de serviços externos continuam exclusivamente no servidor.

## Segurança e conexão

- HTTPS obrigatório, sem redirecionamento automático de requisições autenticadas.
- Tokens de acesso opacos, protegidos pelo Data Protection do ASP.NET Core, com 30 minutos de validade. Renovação por até 14 dias, condicionada à conta e sessão ativas.
- Sessão revogável visível em Minha Conta no site. Senha alterada, sessão encerrada e paciente desativado invalidam o acesso no servidor.
- Tokens cifrados com AES-GCM e chave no Android Keystore; senha não é salva. Backup do aplicativo desabilitado.
- Toda consulta, foto e perfil é obtida a partir do paciente autenticado. IDs de paciente enviados pelo cliente não escolhem outro titular.
- Não há reenvio automático de operações quando a conexão cai. Consulte a lista ou o histórico antes de repetir uma ação com resultado incerto.
- Sem permissão de SMS, contatos, localização ou armazenamento amplo. Foto escolhida pelo seletor do Android.

## Publicar outro APK no site

Copie o APK release para `CallMedCrud/App_Data/Aplicativo/CallMed.apk` e atualize `release.json` na mesma pasta com versão, código, tamanho e SHA-256 reais. Publique os dois juntos. A página `/aplicativo` lê os metadados e `/aplicativo/baixar` serve o arquivo.

A pasta `CallMedTWA` foi preservada como projeto anterior. Ela não participa da compilação do nativo.
