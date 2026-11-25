# Lista de espera e largura do agendamento — 16/11/2026

Base: CallMed-NEXT-V54.1-FLUXOS-TESTADOS.zip.

## Lista de espera

O formulário antigo exibia profissional e especialidade como selects independentes, mas o serviço rejeitava os dois preenchidos ao mesmo tempo. O usuário não tinha uma sequência clara para definir sua preferência.

O novo formulário começa pela especialidade, usando cartões de seleção. Em seguida o paciente escolhe entre qualquer profissional daquela especialidade ou um profissional específico. A opção específica apresenta somente os profissionais ativos da área escolhida. Ao trocar a área, uma seleção incompatível é removida. Data continua opcional, e o período passou a ser escolhido em opções visíveis.

O resumo mostra as preferências antes do envio. O texto deixa explícito que a fila gera ofertas, e não confirma uma consulta automaticamente, e que observações são informações para a equipe, sem se tornarem filtros automáticos adicionais.

O servidor valida o vínculo entre profissional e especialidade. Depois converte a escolha em um único alvo para o serviço existente: especialidade ou profissional. Assim, o novo fluxo evita o conflito entre os campos e preserva a lógica de ofertas, elegibilidade, convênios e duplicidade. POSTs anteriores sem o novo campo de preferência continuam usando a validação anterior. Para a recepção, a seleção de paciente foi mantida.

## Agendamento pequeno no desktop

A classe `.cm19-booking-page` ainda impunha `max-width: 980px`, prevalecendo sobre a largura pretendida para o novo agendamento. A área foi consolidada para ocupar a largura disponível, até 1440 px. O guia lateral agora tem 260 px, e o restante fica para o formulário. Os cartões de especialidade se distribuem conforme a largura disponível. O layout continua empilhado em telas menores.

Cache da PWA: `v108-espera-agendamento`.

## Validação desta rodada

- Compilação Release: zero erros e avisos.
- 37 verificações dedicadas da lista de espera aprovadas, incluindo gravação das duas preferências, exclusão de profissionais inativos, troca de área, rejeição de combinação incompatível no servidor, duplicidade e seleção de paciente pela recepção.
- A mesma bateria verifica a nova largura do agendamento em desktop e a lista de espera nos temas claro/escuro em 320, 390, 768, 1280 e 1713 px, além de fonte ampliada a 200% no celular.
- Fluxo existente de agendamento: 20 verificações aprovadas, incluindo agendar, remarcar e cancelar.
- Revisão visual direcionada ao agendamento do paciente e da equipe: nenhuma falha de carregamento, violação verificada de acessibilidade ou transbordamento detectado.
- Suítes de jornada, aplicação ampla e correções de encaixe: aprovadas.
- Referências das 109 páginas Razor: nenhum vínculo quebrado. Sintaxe das folhas CSS e JavaScript aprovadas.

Evidências textuais: `validation/espera-agendamento/`. O teste dedicado fica em `tests/browser/check-waitlist-flow.cjs` e foi incluído na matriz de navegador da CI. Os dados, contas e profissionais utilizados nos testes são sintéticos; não houve acesso à produção nem envio real de notificações.
