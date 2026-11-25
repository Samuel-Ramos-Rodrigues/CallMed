# Consulta online

## O que causava a tela sem explicação

O link de entrada aparecia mesmo sem autorização do paciente e fora do período de acesso. O endpoint respondia com um único texto HTTP 403 para condições diferentes, sem o layout do CallMed. Essa mensagem não permitia descobrir o motivo específico só pela captura de tela.

A entrada agora abre uma página do CallMed que identifica o estado autorizado da consulta: autorização pendente, espera pelo horário, sala liberada, consulta presencial, consulta encerrada, prazo expirado ou horário inválido. Acesso a consulta alheia apresenta uma página genérica com HTTP 404, sem revelar os dados.

## Do agendamento à sala

1. Ao agendar, o paciente escolhe **Online, por vídeo** ou **Presencial**. Quando a equipe faz a marcação, registra a modalidade escolhida pelo paciente.
2. Paciente e profissional podem conferir a modalidade nos detalhes da consulta. Para um agendamento online, **Acessar consulta online** abre a página de entrada.
3. A recepção organiza as reservas no painel de [salas](SALAS.md). O vídeo é reservado ao paciente e ao profissional vinculados à consulta.

## Paciente

1. Entre com a conta vinculada ao agendamento e selecione **Acessar consulta online**.
2. Leia as informações, marque a autorização e escolha **Autorizar e continuar**. Não é preciso procurar essa autorização em outra tela.
3. Se for cedo, a página informa a data e hora de abertura e verifica a liberação automaticamente. Também existe **Atualizar disponibilidade**.
4. Quando aparecer **Sala liberada**, selecione **Entrar na chamada** e permita câmera e microfone. Aguarde o profissional na mesma página.

O período permanece de 30 minutos antes até 4 horas após o horário agendado, no horário de Brasília. O limite aparece com data completa, inclusive quando atravessa a meia-noite. Após encerramento, cancelamento, ausência ou fim do prazo, o servidor bloqueia o vídeo e explica a situação.

Pré-cadastro, check-in antecipado e perguntas do profissional continuam disponíveis nos detalhes. A participação por vídeo não exige comparecer fisicamente à recepção.

## Profissional

1. Entre com sua conta de profissional e abra a consulta em que está vinculado.
2. Selecione **Acessar consulta online**. Se o paciente ainda não autorizou, a página explica isso e verifica a liberação automaticamente. O profissional não pode consentir pelo paciente.
3. Entre na chamada quando liberada. Confira a identidade do paciente antes de começar o atendimento.
4. Nos detalhes, marque **Conferi a identidade do paciente na chamada e posso iniciar o atendimento** e selecione **Iniciar atendimento**. Para a modalidade online, esse passo dispensa validação presencial pela recepção. Consentimento, período de acesso e bloqueio de atendimentos simultâneos continuam obrigatórios.
5. **Sair da chamada** encerra o uso de câmera/microfone. **Concluir atendimento**, nos detalhes, finaliza também a consulta na agenda. São ações diferentes.

Na modalidade presencial, permanece a validação da recepção antes do início pelo profissional.

## Interface e proteção

- Antes de entrar, aparece uma única ação principal. Controles de microfone/câmera e vídeos aparecem durante a chamada.
- Assistente flutuante e navegação inferior ficam ocultos na página da consulta online para preservar espaço e foco. Os links de retorno continuam disponíveis.
- O servidor verifica autorização em configuração, entrada e sinalização. A recepção/admin não acessam a mídia; outros pacientes/profissionais não acessam consultas alheias.
- O consentimento usa POST com antifalsificação e fica vinculado à consulta. GET não registra consentimento nem início do atendimento.
- Ao sair ou perder autorização, os dispositivos de mídia são liberados. Uma nova entrada ignora sinalização anterior já existente.

## Configuração e testes

A [configuração TURN](../integracoes/METERED-TURN.md) permite a conexão de vídeo em redes que precisam de retransmissão. O [guia do Android](../../mobile/android/README-ANDROID.md) indica onde encontrar a chamada nativa, o candidato de atualização e o procedimento de diagnóstico no aparelho.

Execute a suíte de regras da teleconsulta na raiz do repositório.

```sh
dotnet run --project tests/CallMed.Checks -- --teleconsulta
```
