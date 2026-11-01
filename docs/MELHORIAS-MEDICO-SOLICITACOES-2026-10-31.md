# CallMed — melhorias no médico e nas solicitações (31/10/2026)

## Painel do médico

- Cabeçalho com identificação do médico, especialidade, CRM e atalhos para as seções da própria página.
- Próximo atendimento em destaque, com confirmação/conclusão e acesso aos exames do paciente.
- Indicadores de consultas do dia, pacientes, próximos sete dias e vagas livres.
- Agenda diária com horário, paciente, situação e ações existentes; indicadores não substituem as ações operacionais.
- Andamento do dia calculado a partir das consultas realizadas, sem divisão por zero.
- Semana padrão, próximos pacientes e horários livres/bloqueados com hierarquia visual consistente.
- Todos os formulários continuam POST para as ações existentes. Nenhuma permissão ou controller foi alterado.

## Solicitações do administrador e do atendente

- Página compartilhada pelos dois perfis com resumo das quatro etapas e atalhos para cada coluna.
- Busca e filtro de canal preservados; rótulos acessíveis e opção para limpar filtros.
- Quadro com quatro colunas em telas largas, duas em larguras intermediárias e uma no celular.
- Cartões com protocolo, canal, paciente, especialidade, data de recebimento, preferências, observação e consulta vinculada quando houver.
- Conversa de origem e caminhos de agendamento preservados; pedidos offline e histórico de encerradas continuam disponíveis.
- Contagens identificadas como referentes ao resultado da busca. Aviso quando a lista atinge o limite existente de 400 registros.
- Tela de detalhe com identificação do contato, passos do agendamento, formulário e informações de apoio organizados. Ações de aguardar, cancelar e encerrar permanecem disponíveis no painel expansível.
- Cadastro de solicitações com agrupamento e apresentação coerentes com a tela de detalhe.

## Preservação

A alteração ficou restrita a quatro views, complementos específicos no CSS único, versão do cache e este documento. A folha clássica anterior inteira foi preservada. As regras novas são restritas às telas alteradas. Layout geral, home do paciente, painel inicial de administração/recepção, login, formulário/calendário de consultas, JavaScript de negócio, controllers, serviços, banco e integrações não foram alterados. Cache atualizado para v52.

## Verificações

- CSS completo parseado com tinycss2, sem erros de sintaxe.
- Scripts existentes e service worker conferidos com node --check.
- Formulários, nomes de campos e ações comparados com a base; ícones locais conferidos.
- Prévias derivadas do markup das quatro views, com dados fictícios e CSS real, em Chromium: 390, 768 e 1440 px, claro/escuro, com e sem dados (48 cenários), sem overflow horizontal. Cabeçalhos do detalhe também reconferidos após o ajuste final.
- Conferência visual das prévias desktop e celular. As prévias não executam Razor, autenticação ou PostgreSQL.
- Comparação dos arquivos e integridade do ZIP completo verificadas.

## Limite

Foi tentado o build com SDK .NET 8, mas a execução do SDK/MSBuild foi interrompida por falha do ambiente ao consultar informações do processo. Portanto, compilação da aplicação e navegação com logins reais/banco não estão validadas nesta entrega.
