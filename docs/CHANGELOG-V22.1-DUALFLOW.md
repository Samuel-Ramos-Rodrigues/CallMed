# CallMed V22.1 — DualFlow Edition

## Objetivo
Criar uma versão alternativa da interface sem alterar a lógica de negócio, a arquitetura MVC, o banco, Controllers, Services, Models, integrações ou endpoints existentes.

## Mudanças de UX
- Sidebar desktop deixa de ser permanente e vira **drawer de módulos**.
- Nova **barra superior + ribbon contextual** para navegação principal da equipe.
- Novo **dock flutuante** com ações operacionais frequentes.
- Dashboard de funcionário deixa o modelo de KPIs/cards e passa a representar o **fluxo real do turno**: Entrada → Triagem → Horário → Confirmadas → Atendimento.
- Nova fila de atenção para pendências que podem travar o dia.
- Atendimento omnichannel representado como estado operacional em tempo real.
- Paciente recebe uma UX independente baseada em **jornada de cuidado**, com próximo passo, pedidos e suporte.
- Login apresenta duas perspectivas de acesso (Paciente / Equipe) sem duplicar autenticação; o ASP.NET Identity e as Roles continuam definindo o destino e as permissões.

## Compatibilidade
Toda a lógica V22 de agendamento, triagem, Evolution API, contingência, IA, lista de espera, lembretes, autorização e persistência foi preservada.
