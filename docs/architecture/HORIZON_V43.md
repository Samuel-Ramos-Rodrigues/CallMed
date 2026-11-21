# CallMed Horizon V43 — Auditoria e nova identidade visual

## Objetivo
Atualizar as superfícies dos 109 arquivos Razor e 3 HTML mapeados, com consistência de navegação, tipografia e controles, sem alterar modelos, controllers, banco, canais e automações.

## Identidade
- Paleta azul-petróleo e verde-clínico, com versões claro e escuro.
- Manrope; hierarquia de títulos, conteúdos, ações, bordas e sombras suaves.
- Navegação superior; navegação móvel inferior; sem sidebar permanente.
- Logo horizontal CallMed original preservada.
- Motion reduzido e alto contraste mantidos.

## Arquitetura CSS
- 14 módulos antigos da V42 mantidos para compatibilidade com Razor e JavaScript.
- Dois módulos novos, ativados por body.cm43-horizon:
  - 100-horizon-core.css: cores, perfil, menu, componentes, inputs, tabelas, diálogos, responsividade.
  - 110-horizon-surfaces.css: dashboards dos quatro perfis, central de atendimento, agendamento e calendário, solicitações, relatórios, login, cadastro, landing e offline.
- Links em _DocumentHead.cshtml, offline.html e contingencia.html; PWA atualizado para cache v90-horizon.
- O hash de integridade da folha original de 13 módulos continua protegido em tests/css-integrity.cjs; scripts/check-css.py valida 16 módulos.

## Cobertura funcional
| Contexto | Superfícies |
| --- | --- |
| Público | Home, Aplicativo, Privacidade, Termos |
| Login | Identity Login, Register, ForgotPassword |
| Paciente | Home, Consulta, Jornada, Checkin, MinhaConta, ListaEspera |
| Profissional | MedicoPainel, Medico, Salas, Psicologia, Jornada |
| Atendente | FuncionarioPainel, Agenda, Atendimento, Solicitações, Recepção |
| Administrador | Operação, Relatórios, Configurações, Integrações, Acessos, Auditoria |
| Transversal | Shared layouts, modais, formulários, filtros, tabelas, mensagens |
| Offline | offline.html, contingencia.html |

## Contratos preservados
- Central de atendimento em três colunas, IA com aprovação humana e canais existentes.
- Calendário mensal de sete colunas e regras de disponibilidade.
- Autorização por perfil, login, teleconsulta e filas.
- Nenhuma alteração em Gmail/Brevo, Evolution API, httpsms ou PostgreSQL.

## Validação
- GitHub Actions completo: compilação, testes de domínio e banco, Android, CSS integrity e scanner Razor.
- Suíte browser (design): 48 cenários de URL, perfis simulados, temas, overflow e acessibilidade axe.
- CI não substitui revisão humana no ambiente de homologação, especialmente alto contraste, 200% de zoom e listas extensas.
