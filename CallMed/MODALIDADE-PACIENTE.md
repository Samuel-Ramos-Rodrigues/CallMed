# Modalidade escolhida pelo paciente — CallMed NEXT V54.1

Base: CallMed-NEXT-V54.1-ESPERA-AGENDAMENTO(1).zip, versão mais recente encontrada em 16/11/2026.

## Como usar

1. O paciente abre **Marcar consulta** e escolhe **Presencial** ou **Online, por vídeo**. A seleção é obrigatória no site/PWA e aparece na revisão do agendamento.
2. A modalidade é gravada com a consulta, aparece no comprovante e na preparação e é usada pelas orientações de atendimento. Online não orienta a comparecer à clínica.
3. Para uma consulta futura já marcada, o paciente abre **Acompanhar e preparar consulta → Sua modalidade de atendimento → Salvar minha escolha**.
4. Ao agendar por telefone ou outro contato, a equipe registra a modalidade informada pelo paciente.
5. A recepção organiza sala e duração. Para mudar a modalidade na preparação, precisa marcar que confirmou a mudança com o paciente. O sistema audita autor, modalidade anterior e nova. Sem essa confirmação, o servidor recusa a alteração.

## Regras

- Paciente não acessa a escolha de outra pessoa nem administra salas físicas.
- A modalidade não muda após chegada, início ou encerramento pelo fluxo de escolha do paciente. Após o horário, o paciente deve combinar com a recepção.
- Ao trocar de modalidade, consentimentos online anteriores são invalidados. A escolha de vídeo não substitui o consentimento para a teleconsulta.
- A mudança pelo paciente libera a reserva física anterior. Ao voltar para presencial, a equipe precisa organizar a sala novamente.
- A remarcação mantém a modalidade existente. Consultas legadas sem jornada continuam presenciais até uma escolha explícita.
- O fluxo utiliza Jornada.Tele, que já existe. Nenhuma nova migração de banco foi adicionada.
- Esta alteração oferece as duas modalidades; não acrescenta um cadastro de modalidades permitidas por profissional.

## Android

O código-fonte nativo inclui a escolha na revisão do agendamento e envia a modalidade à API. A API grava a mesma jornada usada pelo site. Clientes antigos que não enviam esse campo mantêm o padrão presencial por compatibilidade. O APK de teste atual, com o refinamento visual e a escolha de modalidade, está em artifacts/android. Consulte CORRECAO-VIDEO-APK-V54.1.2.md. O APK de produção que já existia no download do site permanece anterior. Consulte APK-VISUAL-V54.1.1.md para instalação e assinatura.

## Verificação

- Compilação de CallMed.Checks e dependências Web/Application/Persistence/Domain: 0 erros e 0 avisos.
- 51 verificações de salas/teleconsulta, incluindo escolha, autorização, auditoria e orientações.
- 37 verificações de jornada.
- 126 verificações da API móvel, incluindo persistência da modalidade online no agendamento.
- 4 verificações HTTP do site: agendar online, ver no comprovante, mudar para presencial, recusar troca sem concordância e aceitar troca combinada (agendamento/comprovante contados juntos).
- Auditoria estrutural: 0 problemas. Sintaxe JavaScript do agendamento: válida.
- Testes de serviço e HTTP usam banco InMemory e dados fictícios; não executados contra PostgreSQL de produção. Não foi possível executar a inspeção visual em Chromium neste ambiente; o Razor foi compilado e as páginas foram verificadas por HTTP. O código Android foi compilado posteriormente no refinamento visual V54.1.1; consulte APK-VISUAL-V54.1.1.md.
