# CallMed V21.6.5 — Interface System Refactor

## Objetivo
A V21.6.5 refatora a experiência visual em nível de **Razor/HTML e CSS**, substituindo a estratégia anterior de apenas sobrepor estilos globais.

## Alterações
- novo app shell `cm25` com sidebar, topbar e área de conteúdo revisadas;
- cabeçalhos de página estruturados com contexto, ícone e ações;
- telas CRUD migradas para painéis, grids de detalhe e barras de ação consistentes;
- formulários de paciente, médico, funcionário, especialidade e acesso reorganizados;
- telas de exclusão/desativação transformadas em confirmações de alto risco;
- Central de Atendimento recebe estrutura visual própria para lista, conversa, contexto e composer;
- dashboards, kanban, agenda, relatórios e configurações passam a compartilhar o novo design system;
- Identity e páginas públicas atualizadas para o mesmo idioma visual;
- design responsivo e dark mode revistos;
- identidade cromática: índigo, violeta e azul, sem verde como cor principal.

## Compatibilidade
As rotas, `asp-*`, models, controllers, validações e serviços foram preservados. A mudança desta release é majoritariamente de apresentação/estrutura de view.
