# Central de Atendimento: e-mail com menos avisos

## Organização

- O andamento da IA ocupa uma barra compacta. As etapas continuam sendo
  atualizadas em tempo real, com indicador de processamento.
- A descrição adicional aparece nas falhas, interrupções e solicitações da
  equipe. Nos demais estados, o título informa o andamento sem repetir instruções.
- "Gerar rascunho" aparece quando há uma mensagem disponível para análise.
  O controle continua no DOM e acompanha o estado recebido do servidor.
- O rascunho usa um rodapé neutro com "Rascunho · não enviado", "Aprovar e enviar"
  e "Descartar", sem a caixa amarela e o parágrafo repetido em cada mensagem.
- A explicação sobre o canal fica em "Como funciona o e-mail", expansível na
  área lateral. Em telas menores, essa área acompanha a organização responsiva
  existente.
- O cabeçalho evita repetir o endereço quando ele já é o nome exibido do contato.
- O cache do PWA foi atualizado para v61.

## Correção da explicação de identidade

O aviso genérico "Contato ainda não identificado" não aparece mais no centro
das conversas de e-mail. Ele sugeria que o vínculo com o paciente liberaria
agendamentos para a IA, o que não corresponde às permissões desse canal.

A explicação lateral esclarece que o vínculo organiza o histórico, não autentica
o remetente e não libera alterações de agenda para a IA. A IA consulta informações
públicas e prepara rascunhos. A aprovação envia somente a mensagem; agendamentos
e alterações são concluídos pela recepção ou pelo portal autenticado.

## Validação

- Compilação .NET 8 concluída sem erros, com os dois avisos já existentes
  CS8602 em ForgotPassword e CS8321 em Atendimento/Index.
- 198 verificações de backend aprovadas.
- Testes de navegador de andamento, conexão, aprovação, descarte e reenvio
  aprovados com integrações simuladas.
- Layouts inspecionados em 390, 768 e 1440 pixels, nos temas claro e escuro,
  incluindo os rascunhos, a ajuda expansível e os controles de ação.
- Conferida a preservação do formulário durante as atualizações, para manter a
  confirmação de envio funcionando.

Publicar esta versão no Render para aplicar a organização à interface.
