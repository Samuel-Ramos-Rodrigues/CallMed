# CallMed Android nativo — 23.1.0

Aplicativo de pacientes em Kotlin e Jetpack Compose. As telas são componentes Android; não há WebView, Custom Tab de navegação nem dependência do TWA. O site ASP.NET Core continua sendo o servidor e usa o mesmo banco.

## Primeiro uso

1. Publique a pasta `CallMedCrud` desta entrega no Render. A API nova fica em `https://callmed.onrender.com/api/mobile/v1`.
2. Instale o APK disponibilizado em `/aplicativo` no site atualizado. Ele também está em `CallMedCrud/App_Data/Aplicativo/CallMed.apk` neste projeto.
3. Entre com CPF ou e-mail e a senha de uma conta **Paciente** já cadastrada. Contas da equipe e contas com autenticação em dois fatores não são suportadas pelo cliente móvel.

O servidor precisa desta atualização antes do primeiro login nativo. Nenhuma variável nova de ambiente ou migração adicional foi criada para o app; as migrações já existentes, incluindo as tabelas de chaves e sessões, continuam necessárias.

## Funções disponíveis

- Quatro áreas fixas: **Início, Consultas, Chat e Conta**. Próxima consulta, histórico e atalhos usam dados da conta autenticada.
- Agendamento em cinco etapas: **Especialidade → Médico → Data → Horário → Revisão**, com primeira vaga disponível, elegibilidade do convênio e confirmação explícita antes de gravar.
- Remarcação começa na data, preservando médico e forma de atendimento. Confirmação e cancelamento seguem as regras do site. Reservas e cancelamentos atualizam também a solicitação vinculada e a auditoria.
- **Leitura** acessível em todas as telas: texto normal/grande/extra, alto contraste, mais espaço, menos animações e tema claro/escuro/do sistema. A fonte nunca fica menor que a escala escolhida no Android. Papéis, títulos, estados e descrições estão definidos para leitores de tela.
- Ajustes de leitura funcionam sem internet e mantêm os formulários preenchidos ao abrir/fechar o painel. Preferências compatíveis sincronizam com o site; a escala extra é local ao aparelho.
- Perfil com foto JPG/PNG até 2 MB, nome, telefone, nascimento e convênio; alteração de e-mail e senha confirmadas pela senha atual. CPF fica em leitura.
- Dispositivos conectados: consulta e encerramento dos outros acessos, mantendo o atual. E-mail e senha renovam os tokens do aparelho atual e revogam os outros acessos.
- Exames: registros aprovados e ativos do próprio paciente, com nome, data e local. O sistema atual não armazena laudos PDF; o app não cria documentos fictícios.
- Chat com o histórico do site e pausa da IA durante atendimento humano. Ao encerrar, oferece avaliação opcional; nota e comentário aparecem após o aceite.

**Criar conta** e **Esqueci minha senha** abrem o navegador com os fluxos existentes do site. O app não sincroniza cookies com o navegador. Gestão da equipe, notificações push próprias, cadastro nativo e operações clínicas offline não estão incluídos. As preferências de SMS/e-mail/WhatsApp continuam controlando os avisos do servidor; não ativam push Android.

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

Para testes do cliente e das telas Compose: `./gradlew testDebugUnitTest`. Os testes de interface usam Robolectric, sem depender de um emulador conectado. Para análise Android: `./gradlew lintRelease`.

O release usa R8 para reduzir código e recursos. O mapa de ofuscação deste APK está em `release/mapping-23.1.0.txt.gz`; descompacte-o para analisar um relatório de falha desta versão. O mapa anterior foi preservado para diagnóstico da versão 23.0.0.

O APK release usa `com.callmed.app`, código **2310**, versão **23.1.0**, Android 8.0 ou superior. A assinatura usa a mesma chave que já acompanhava o projeto; permite atualizar a instalação direta anterior sem desinstalar. Uma instalação distribuída pela Play Store com outra chave exige verificar o certificado correspondente.

`keystore.properties` aponta para a chave local. Variáveis opcionais para CI: `CALLMED_KEYSTORE_FILE`, `CALLMED_KEYSTORE_PASSWORD`, `CALLMED_KEY_ALIAS`, `CALLMED_KEY_PASSWORD`. Não coloque esses arquivos nem senhas em repositórios públicos. O Render não precisa da chave para servir o APK já assinado; o contexto Docker exclui ambas as pastas Android.

O host está em `app/build.gradle` (`BuildConfig.API_BASE_URL`). Se mudar de domínio, atualize esse valor e gere outro APK. Credenciais de serviços externos continuam exclusivamente no servidor.

## Segurança e conexão

- HTTPS obrigatório, sem redirecionamento automático de requisições autenticadas.
- Tokens de acesso opacos, protegidos pelo Data Protection do ASP.NET Core, com 30 minutos de validade. Renovação por até 14 dias, condicionada à conta e sessão ativas.
- Sessão revogável visível na Conta do app e em Minha Conta no site. Alterações de e-mail/senha pelo app invalidam os tokens antigos, retornam tokens novos para a sessão atual e revogam os outros acessos. Sessão encerrada ou paciente desativado perde o acesso.
- Tokens cifrados com AES-GCM e chave no Android Keystore; senha não é salva. Backup do aplicativo desabilitado.
- Consultas, exames, foto, perfil e dispositivos são obtidos a partir do paciente autenticado. IDs de paciente enviados pelo cliente não escolhem outro titular.
- Não há reenvio automático de operações quando a conexão cai. Consulte a lista ou o histórico antes de repetir uma ação com resultado incerto.
- Sem permissão de SMS, contatos, localização ou armazenamento amplo. Foto escolhida pelo seletor do Android.

## Publicar outro APK no site

Copie o APK release para `CallMedCrud/App_Data/Aplicativo/CallMed.apk` e atualize `release.json` na mesma pasta com versão, código, tamanho e SHA-256 reais. Publique os dois juntos. A página `/aplicativo` lê os metadados e `/aplicativo/baixar` serve o arquivo.

A pasta `CallMedTWA` foi preservada como projeto anterior. Ela não participa da compilação do nativo.

## Homologação no aparelho

Build, 28 testes JVM/Compose e verificações do servidor passaram nesta entrega. As telas foram renderizadas em 320dp, com fonte normal e 200%. Ainda homologue em um celular físico: TalkBack e ordem de foco, teclado, seletor de foto, rotação, calendário, rede móvel e atualização por cima da instalação anterior. Veja `docs/validacao/ANDROID-NATIVO.md` na raiz do projeto.
