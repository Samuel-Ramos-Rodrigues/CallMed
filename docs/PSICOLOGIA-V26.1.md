# CallMed — revisão V26.1 / Android 26.1.0

Esta revisão continua a V26 e corrige o comportamento do app ao recuperar chaves, trocar de conta, interromper ou reiniciar uma chamada. O fluxo de Psicologia e a integração FHIR da V26 permanecem como base.

## Correções

| Situação | Comportamento corrigido |
|---|---|
| Cofre de chave ilegível no aparelho | A tela mantém a restauração acessível e explica o problema. Não gera outra chave sobre o cofre danificado. |
| Senha errada ou arquivo inválido | A tentativa termina sem registrar/substituir a chave; os controles de recuperação voltam a ficar disponíveis. |
| Cancelar o seletor de arquivos | A senha temporária é limpa; a chave já guardada é mantida. |
| Abrir o seletor ou girar a tela | A operação pendente pertence ao ViewModel da conta. Os textos clínicos saem da tela enquanto ela está em segundo plano; a transferência esperada pode concluir no retorno. |
| Sair da consulta antes de o arquivo retornar | A senha pendente é apagada e o resultado atrasado é ignorado. |
| Trocar de conta durante uma operação | Os pedidos clínicos ficam vinculados à sessão que os iniciou; uma chave antiga não é cadastrada com os tokens de outro login. |
| Sessão revogada ou expirada | As áreas clínicas e de privacidade avisam o estado central da conta, que limpa o conteúdo e retorna ao login quando o acesso deixou de existir. |
| Entrar rapidamente duas vezes na chamada | Só uma preparação de mídia pode começar. |
| Falha atrasada da tentativa anterior | O evento antigo não encerra a tentativa nova. SDP, ICE, foco de áudio e o sinal de saída ficam associados à tentativa de origem. |
| Saída da chamada | Os renderizadores são desconectados antes de destruir o receptor WebRTC; câmera, microfone, conexão e foco de áudio são liberados. |
| Fones conectados | No Android 12 ou superior, a seleção usa a API de comunicação e prioriza o dispositivo externo conectado. No Android anterior, o app deixa de forçar o alto-falante sobre fones reconhecidos. |
| Dispositivo de áudio removido | A chamada é encerrada ao detectar a remoção do dispositivo escolhido, orientando a conferir o áudio antes de entrar novamente. |
| Internet temporariamente indisponível na preparação da sala | A atualização de situação é tentada novamente enquanto a tela está em primeiro plano. |

## Instalação e servidor

- Versão Android **26.1.0**, código **2601**, pacote `com.callmed.app`, Android mínimo 8.0, com o mesmo certificado da instalação anterior.
- O app é compatível com os endpoints do backend V26. Se sua instalação ainda é anterior à V26, publique o projeto completo antes de usar as novas áreas clínicas.
- Publique `CallMedCrud/App_Data/Aplicativo/CallMed.apk` e `release.json` juntos para disponibilizar esta versão no site.
- **Não há variável de ambiente, credencial ou patch de banco novo nesta revisão.** O banco continua usando o V33 já incluído na V26.
- As chaves clínicas existentes no aparelho, suas cópias protegidas e os arquivos de assinatura mantêm os formatos anteriores.

## Validação em aparelho

Os testes automatizados usam cadastros e chaves fictícios. O roteamento por fabricante, Bluetooth, áudio/vídeo em redes reais, permissões, Keystore, seletor de arquivos e TalkBack continuam exigindo o roteiro físico de `ACEITACAO-V26.md`.

Ao testar esta revisão, inclua: restaurar uma cópia do site com senha certa e errada; cancelar o seletor; girar a tela com o seletor aberto; sair e entrar de novo na sala; conectar/remover os fones; interromper com outra ligação; e revogar a sessão pelo site enquanto o app está aberto. Confira também se a chamada anterior não interfere na seguinte.

A homologação com o PEP escolhido, a aprovação clínica e de governança e o piloto com dados reais continuam externos. Esta revisão não certifica esses itens.

## Referências técnicas

O roteamento usa `AudioManager.setCommunicationDevice` e `clearCommunicationDevice` no Android 12+, conforme a [referência oficial do Android](https://developer.android.com/reference/android/media/AudioManager). A manutenção das chaves continua usando o formato interoperável da V26; o servidor recebe somente a chave pública e envelopes clínicos cifrados.
