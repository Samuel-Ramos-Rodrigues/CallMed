# CallMed — organização visual e layouts

**Arquitetura visual atualizada em:** 29/10/2026.  
**Fonte visual:** [`CallMedCrud/wwwroot/css/callmed.css`](../CallMedCrud/wwwroot/css/callmed.css).

O código Razor existente foi usado para mapear a navegação e a estrutura de cada módulo. O visual foi reconstruído de forma unificada, sem reaproveitar as regras das antigas folhas de estilo.

## Organização de tela

- Layout autenticado: cabeçalho adaptável, navegação principal, acesso ao menu de módulos, preferências de aparência/acessibilidade, conta e navegação móvel.
- Painel da equipe: mensagem de boas-vindas, ação de agendar, acesso rápido, próximos atendimentos, resumo, pontos de atenção e ocupação.
- Atendimento: inbox omnichannel, conversa, ações operacionais, mensagens e contexto do paciente.
- Agenda: navegação temporal, filtros, métricas, calendário e estados de consulta.
- Área do paciente: próxima consulta, atalhos, pedidos, agenda e modo simples.
- Site público e identidade: homepage, privacidade, login, cadastro por etapas, consentimento de termos e orientação de senha.
- Demais módulos: pacientes, médicos, funcionários, especialidades, convênios, disponibilidade, solicitações, exames, lista de espera, relatórios, auditoria, integrações, configurações e conta.
- Páginas especiais: contingência/offline e impressão de comprovante.

## Arquivos de referência

| Arquivo | Responsabilidade |
|---|---|
| `CallMedCrud/wwwroot/css/callmed.css` | Único CSS de produção: tokens, temas, componentes, módulos, responsividade e impressão |
| `CallMedCrud/Views/Shared/_DocumentHead.cshtml` | Metadados e carregamento do CSS comum |
| `CallMedCrud/Views/Shared/_Layout.cshtml` | Shell autenticado e navegação por perfil |
| `CallMedCrud/Views/Shared/_PublicLayout.cshtml` | Shell do conteúdo público |
| `CallMedCrud/Views/Shared/_AuthLayout.cshtml` | Shell da autenticação e rodapé legal |
| `CallMedCrud/wwwroot/js/preferences.js` | Aplica as preferências antes da primeira pintura |
| `CallMedCrud/wwwroot/js/theme.js` | Mantém a seleção de tema e atualiza a cor do navegador |
| `CallMedCrud/wwwroot/service-worker.js` | Pré-carrega o CSS atual e invalida o cache anterior |

As 13 folhas de estilo anteriores foram substituídas por `callmed.css`. As views e seus seletores foram mantidos para reduzir risco de alteração de regra de negócio ou navegação. Os relatórios anteriores estão mantidos como histórico; a checagem do pacote atual está documentada em [`REDESIGN-VISUAL-2026-10-29.md`](REDESIGN-VISUAL-2026-10-29.md).

## Antes da publicação

A checagem estática confirmou referências CSS válidas, sintaxe CSS parseável e manifesto JSON válido. Ainda é necessário abrir `CallMed.sln` no Visual Studio, executar a compilação .NET e conferir as telas reais nos modos claro e escuro, em desktop e celular. A compilação não foi executada neste ambiente porque o SDK .NET não estava instalado.
