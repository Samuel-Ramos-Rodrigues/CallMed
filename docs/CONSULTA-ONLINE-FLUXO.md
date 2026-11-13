# Consulta online — fluxo revisado em 07/11/2026

## O que causava a tela sem explicação

O link de entrada aparecia mesmo sem autorização do paciente e fora do período de acesso. O endpoint respondia com um único texto HTTP 403 para condições diferentes, sem o layout do CallMed. Essa mensagem não permitia descobrir o motivo específico só pela captura de tela.

A entrada agora abre uma página do CallMed que identifica o estado autorizado da consulta: autorização pendente, espera pelo horário, sala liberada, consulta presencial, consulta encerrada, prazo expirado ou horário inválido. Acesso a consulta alheia apresenta uma página genérica com HTTP 404, sem revelar os dados.

## Preparação pela clínica

1. Em **Acompanhar**, abra **Detalhes e preparação** da consulta.
2. A recepção ou administração escolhe **Modalidade do atendimento → Online, por vídeo** e salva. A sala física é usada apenas para atendimento presencial.
3. Paciente e profissional veem a modalidade na lista. O botão **Acessar consulta online** leva diretamente à página de entrada. A recepção recebe apenas orientações de organização, sem acesso ao vídeo.

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

## Atualização e verificação

Esta correção é do backend, das páginas e do JavaScript. **O APK continua 24.0.0 e não precisa ser recompilado ou reinstalado. Não há coluna ou patch de banco adicional.** O V31 e o modo coturn com segredo compartilhado permanecem. Para Metered, esta revisão acrescenta as opções `Clinica__Tele__TurnUsername` e `Clinica__Tele__TurnPassword`; veja [METERED-TURN.md](METERED-TURN.md).

Atualize o backend completo. Se o repositório contém arquivos extras de outra versão, mantenha uma cópia fora do repositório e substitua a pasta que contém `CallMedCrud.csproj` pelo conteúdo completo desta entrega. A correção de `SalaAtendimento` continua incluída.

Verificação específica reproduzível:

```sh
dotnet run --project tests/CallMed.Checks -- --teleconsulta
```

Foram executados 21 testes específicos de regras/permissões/horários, incluindo limite exato, expiração e meia-noite, além das verificações de regressão da jornada e dos canais. O teste de navegador exercita autorização pelo formulário real, espera com liberação automática para o profissional, proteção de acesso e conexão WebRTC com mídia fictícia.

Para Metered ou outro provedor com credenciais de usuário/senha, veja [METERED-TURN.md](METERED-TURN.md); essa revisão acrescenta os dois campos opcionais no ambiente.

A conexão entre redes distintas ainda depende de STUN/TURN configurado. A correção da interface não fornece essa infraestrutura nem representa homologação clínica/regulatória. Não houve acesso ao banco de produção, envio de mensagens reais ou deploy no Render do usuário.
