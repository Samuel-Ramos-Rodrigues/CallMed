# APK 54.1.2 — correção da entrada na videochamada

## Relato e diagnóstico

O usuário informou que o APK fechou ao tocar em Entrar na chamada. Sem o Logcat do aparelho, não é possível afirmar a causa exata desse fechamento.

A revisão identificou um caminho inseguro: o estado de chamada ativa era publicado antes de validar a sala e inicializar a biblioteca nativa, permitindo que a composição criasse renderizadores/EGL durante a preparação. Falhas de renderização ficavam fora do tratamento de erros da entrada; LinkageError também não era tratado. A flag de biblioteca inicializada era marcada antes de a inicialização concluir.

## Alterações

- Separação entre entrada em preparação e vídeo pronto para renderizar.
- Indicador de preparação e bloqueio de toques repetidos enquanto a entrada está pendente.
- Biblioteca nativa marcada como inicializada somente depois de carregar com sucesso.
- Conferência de câmera e microfone antes de inicializar os recursos nativos.
- Erros recuperáveis de carregamento/renderização são tratados e mostrados na tela, com códigos VIDEO-INIT/VIDEO-RENDER para diagnóstico.
- Tentativas antigas não alteram uma nova entrada; recursos e estado de áudio são liberados em falhas e saídas.
- Liberação final idempotente para evitar reutilizar uma instância já encerrada.
- Opção Abrir consulta no navegador usa o acesso seguro que já existia no aplicativo.
- Versão 54.1.2-debug, código 5412; aparência e demais funções da versão anterior preservadas.

## Instalação e teste no aparelho

Instale CallMed-V54.1.2-Correcao-Video-TESTE.apk sobre o APK de teste anterior. Foram conferidos o mesmo pacote com.callmed.app.debug e a mesma assinatura da versão 54.1.1, permitindo atualização sem desinstalar essa edição.

Abra a consulta online, aceite o consentimento quando solicitado e toque em Entrar na chamada. Durante a preparação, a interface deve mostrar o progresso; vídeo e controles só aparecem depois da inicialização. Em caso de erro tratado, use a mensagem/código exibido e a opção de navegador.

Esta é uma edição de teste. O APK oficial de produção possui sua própria assinatura. O novo APK também está em artifacts/android; o APK de produção do download do site não foi substituído por um APK debug.

## Verificação

- Compilação Android: aprovada.
- 88 testes aprovados, sem falhas, incluindo carregamento nativo simulado com UnsatisfiedLinkError, repetição da tentativa, permissão revogada, renderização antes de estar pronto e concorrência de tentativas.
- Assinatura do APK: válida e igual à do APK de teste anterior.
- Alinhamento do APK para páginas de 16 KB: validado com zipalign.
- Bibliotecas nativas arm64, armv7 e x86_64 presentes.
- Não houve teste da câmera/GPU ou chamada real no aparelho do usuário. Os testes Robolectric não substituem essa verificação. Se o aplicativo continuar fechando, o Logcat do aparelho é necessário para confirmar a causa específica, especialmente em uma falha nativa do sistema de vídeo.

O projeto mantém as correções web da revisão de telas e avaliação. Nenhuma alteração do banco ou do servidor foi necessária para este ajuste Android.
