# CallMed — Auditoria de consistência visual V39 → V40

**Origem:** inspeção estática da `master` de 14/11/2026 (`5e3b674`) e avaliação dos testes já existentes.  
**Escopo:** divergências de apresentação (componentes feios, não condizentes, difíceis de ler), não erros de regra de negócio.  
**Importante:** presença de uma classe sem seletor específico é evidência de ausência de estilo *próprio*, não prova de uma falha visual perceptível em todo estado. Aprovação de CI não comprova qualidade estética pixel a pixel.

## Diagnóstico principal

- **109 views Razor** e apenas **uma folha CSS** (`src/CallMed.Web/wwwroot/css/callmed.css`, ~208 KB e 8.658 linhas na V39).
- A folha contém sucessivas gerações de classes (`v16-*`, `cm19-*`, `cm25-*`, `j-*`, `cm37-*`) e ajustes posteriores V36–V39. Há diversos seletores redefinidos e dezenas de media queries.
- O bloco `@scope (body.identity-body)` ocupa aproximadamente as linhas **6365–8076**. Estilos antigos de solicitações presentes nesse bloco se aplicam à identidade/login, mas **não** à área autenticada `body.cm39-shell` que utiliza os componentes. Exemplos: `.cm-requests-screen .cm-requests-hero-icon` e `.cm-requests-offline-icon`.
- Outras classes renderizadas por `Solicitacoes/Index.cshtml` e `Solicitacoes/_Card.cshtml` sequer possuíam regra CSS global específica: `.cm-requests-board`, `.cm-request-column`, `.cm-request-card`, `.cm-request-person`, `.cm-request-preferences`, `.cm-requests-filters`, entre outras.
- `Jornada/Index.cshtml` e `Jornada/Consulta.cshtml` usam classes como `.cm-journey-toolbar`, `.cm-journey-section`, `.cm-journey-step` e `.cm-journey-next` sem regras próprias correspondentes.
- As views de `Paciente/Index`, `Medico/Index` e `Consulta/Index` continuam misturando `v16` e `cm25`. Seus cartões e tabelas funcionam com estilos genéricos, mas nem todos os subcomponentes possuem apresentação coerente.
- `Organizacao/Index.cshtml` ainda abria por padrão o grupo `Rotina`, embora a navegação tenha renomeado o grupo prioritário para `Hoje`.

## Matriz de correção

| Prioridade | Tela | Evidência no código | Tratamento |
|---|---|---|---|
| P0 | Solicitações | Quadro de três etapas, cartões e filtros sem classes de layout efetivas | **V40 — corrigido**, estilos próprios scoped em `body.cm39-shell` |
| P1 | Jornada | Barra, seções, ações, ofertas e passos com estilo incompleto | **V40 — corrigido**, hierarquia e estados visuais |
| P1 | Cadastro de pacientes/profissionais | Busca e tabelas `v16` não harmonizadas | **V40 — refinado**, busca, tabela e indicadores mantidos |
| P1 | Consultas | Filtros antigos e tabela ampla com estilos genéricos | **V40 — refinado**, formulário e rolagem controlada |
| P1 | Catálogo de funções | Grupo `Hoje` não abria por padrão | **V40 — corrigido** |
| P2 | Histórico de exames | `.cm25-exam-record` não possui seletor próprio; conferir listas e status | Inspeção visual futura |
| P2 | Minha conta | Blocos de tema e convênio parcialmente herdados de estilos genéricos | Inspeção visual futura |
| P2 | Integrações / Configurações | Componentes `cm19` em páginas V39 | Harmonização posterior, sem alterar estados ou formulários |
| P2 | Agendamento e disponibilidade | Formulários `cm19` válidos, mas diferentes da V39 | Inspeção visual, sobretudo seletor de data e celular |
| P2 | Prontuário/psicologia | Layouts próprios com classes não especializadas | Preservar segurança; revisar apenas UI |
| P3 | CSS global | Regras acumuladas, colisões e tokens de gerações distintas | Refatorar por grupos lógicos sem alterar CSS carregado ou funcionalidades |

## Critérios de validação

1. GitHub Actions backend/PostgreSQL, Android, certificados e navegação/browser intactos.
2. Inspeção automatizada `check-design.cjs` agora verifica `display:grid` em solicitações, `display:flex/grid` na Jornada e `min-width` legível nas tabelas.
3. Testes em navegador de **tema claro e escuro** e **390px com fonte 200%** devem permanecer sem transbordamento e sem violações de contraste.
4. Revisão humana obrigatória para julgar *beleza, alinhamento, densidade, clareza* nos quatro perfis. Testes automatizados não garantem isso.
5. Preservar login V34, Central de Atendimento em três colunas, teleconsulta em destaque e campos/ações existentes. Não alterar controllers, banco, automações ou acessibilidade.

## Próxima auditoria recomendada

Gravar capturas comparáveis de: Paciente (início, agenda, exames), Médico (início, consulta, prontuário), Recepção (início, solicitações, Jornada, Central) e Admin (indicadores, acessos, integrações, configurações). Conferir desktop 1440px, tablet 820px e móvel 390px, nos dois temas, com dados reais de teste e estados vazio/carregado/erro.

**Publicação:** a V40 deve permanecer em PR separada até a CI e revisão visual; não aplicar automaticamente à `master` ou Render.
