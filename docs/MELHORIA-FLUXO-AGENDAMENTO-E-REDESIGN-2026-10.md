# CallMed — fluxo sem triagem de consultas, revisão de exames e redesign

## Fluxo de consultas

- O fluxo administrativo não possui mais rota, ação ou tela `Solicitacoes/Triagem`.
- A equipe recebe a solicitação, organiza paciente/especialidade/médico de preferência e segue para selecionar um horário.
- A consulta é confirmada pelo `AgendamentoService` quando a reserva do horário é gravada com sucesso; validação de disponibilidade, convênio selecionado e conflitos continua ativa.
- Solicitações antigas com `StatusSolicitacaoAtendimento.EmTriagem` aparecem como “Recebida”. O enum e as colunas de triagem legadas são mantidos somente para compatibilidade com dados já gravados; não existe uma etapa ativa de triagem para novas consultas.
- O formulário de médico de preferência valida que o médico pertence à especialidade escolhida.

## Revisão administrativa de exames

- Apenas perfis `Funcionario` e `Admin` podem cadastrar novos exames, aprovar, rejeitar ou desativar registros.
- Cada novo registro é forçado no servidor para `StatusTriagem = Pendente`; o servidor ignora qualquer tentativa do navegador de definir esse status.
- Somente registros ativos e aprovados são exibidos no histórico do paciente/médico.
- Rejeição exige motivo; aprovação/rejeição grava usuário, data e observação e registra auditoria.
- A ação de desativação também exige perfil `Funcionario` ou `Admin` (não depende apenas de o botão estar escondido na tela).
- O patch V24 preserva registros antigos como aprovados e deixa o padrão dos novos registros como pendente.
- Esta triagem é administrativa (conferência do registro), não interpreta o resultado clínico do exame.

## Identidade visual e layout

- `wwwroot/css/clinic-redesign.css` passa a ser carregado nos layouts interno e público depois dos estilos base.
- Área autenticada: sidebar de navegação com ícones SVG, fundo clínico claro, cartões e formulários com hierarquia visual, estados e alertas padronizados.
- Em telas pequenas, a sidebar se converte em cabeçalho compacto; a navegação inferior móvel permanece disponível.
- Área pública: cabeçalho, hero e ilustração ajustados para mobile e desktop.
- Logo é mostrada sem placa/fundo branco; os PNGs permanecem transparentes.
- Service worker atualizado para invalidar o cache anterior e armazenar `clinic-redesign.css`, evitando o layout antigo após atualizar o PWA.

## Homologação recomendada

1. Criar uma nova solicitação administrativa e confirmar que ela abre Detalhes/Organizar agendamento (sem etapa de triagem).
2. Selecionar especialidade e médico de outra especialidade; o servidor deve rejeitar a combinação.
3. Agendar uma vaga disponível; a consulta deve ficar Confirmada na transação de agendamento.
4. Cadastrar um exame novo; confirmar que fica pendente e não aparece no histórico do paciente.
5. Aprovar o exame como funcionário/admin; confirmar que passa a aparecer no histórico.
6. Rejeitar um exame sem motivo; a operação deve ser recusada. Repetir com motivo e confirmar que fica fora do histórico.
7. Tentar POST direto a `Desativar` como paciente ou médico; deve retornar proibido.
8. Testar layout desktop e mobile, navegação lateral/compacta, alertas, login, agenda, solicitações e histórico de exames.

> Limitação do ambiente de edição: o SDK .NET não estava disponível, então compile/teste o projeto no CI/Render antes da publicação.
