# Atendimento administrativo automático por e-mail

Esta entrega substitui a aprovação obrigatória das respostas administrativas
por envio automático no modo IA. Mantém a conclusão do agendamento no portal
autenticado. A organização visual compacta da Central foi preservada.

## Fluxo

1. A Gmail API registra a mensagem recebida na Central.
2. A triagem recebe a pergunta e o histórico realmente trocado. Pedir para marcar
   com um médico é tratado como busca de opções, sem reservar um horário.
3. O mesmo agente do site pode usar somente cinco ferramentas públicas no e-mail:
   médicos, horários de uma data, próximas datas, opções de agendamento e
   informações da clínica. A validação do backend também bloqueia outras funções.
4. A resposta administrativa é enviada pelo SMTP automaticamente. As vagas
   retornadas pela busca têm links para abrir o formulário com médico, data e
   horário preenchidos. Os links não incluem pacienteId nem permitem alterar a
   agenda sem autenticação e confirmação no portal.
5. Pedidos para efetivar operações privadas recebem uma orientação fixa para a
   área autenticada. Identificadores sensíveis não são repassados ao Gemini nem
   repetidos na resposta. Pedidos explícitos de atendente e assuntos clínicos
   seguem para a equipe, com uma confirmação de recebimento padronizada.

As respostas curtas continuam usando o histórico. Rascunhos não enviados não
são reconstruídos como falas recebidas pelo paciente. Entradas por webhook/alias
também usam essa política, preservando a validação do remetente da conversa.

## Controle humano e retomada

- Assumir a conversa pausa a automação. A rotina não toma o atendimento de um
  responsável e verifica novamente o contexto antes de enviar uma resposta.
- Gerar rascunho é uma ação manual e mantém revisão e aprovação. Uma falha nessa
  ação não se transforma em envio automático da mesma mensagem em segundo plano.
- Rascunhos que já estavam aguardando aprovação não são enviados pelo serviço
  de retomada.
- A cada 30 segundos, uma rotina procura entradas recentes ainda sem resposta.
  O padrão é uma janela de 24 horas, com lotes de até 20 entradas.
- Pendências da política anterior marcadas como equipe/falha, sem responsável,
  podem ser reavaliadas uma vez pelas regras novas. Pedidos explícitos de
  atendente permanecem na fila humana. Mensagens antigas e conversas encerradas
  ficam fora dessa retomada.
- Falhas de análise têm até três tentativas por mensagem, com pelo menos um
  minuto entre novas tentativas. Uma geração interrompida há mais de dez minutos
  pode ser retomada. Uma nova entrada renova o limite.
- O envio usa a chave única por entrada e o controle de concorrência existente.
  Uma resposta de transporte sem confirmação não é retransmitida automaticamente.
  O resultado do envio continua sendo aceitação pelo servidor, não prova de
  entrega ou leitura na caixa do destinatário.
- O filtro de mensagens automáticas do Gmail foi preservado. O SMTP solicita
  supressão de respostas automáticas por cabeçalho, reduzindo ciclos entre robôs.

## Configuração

| Variável | Padrão | Finalidade |
|---|---|---|
| `Atendimento__Email__EnviarRespostasAutomaticamente` | `true` | Envio administrativo automático no modo IA. `false` mantém revisão dos rascunhos. |
| `Atendimento__Email__RetomarPendencias` | `true` | Habilita a rotina de retomada. |
| `Atendimento__Email__JanelaPendenciasHoras` | `24` | Idade máxima das entradas da retomada, limitada entre 1 e 72 horas. |
| `Atendimento__Email__PortalBaseUrl` | vazio | Origem HTTPS pública do portal. Vazio usa `RENDER_EXTERNAL_URL` e, depois, `Twa:Origin`. |

As credenciais de Gmail, Gemini e SMTP continuam necessárias. Confirme que o
domínio dos links corresponde à clínica. Para domínio próprio, configure
`Atendimento__Email__PortalBaseUrl` com a origem correta. Links do portal não
substituem a autenticação nem garantem que a vaga continue disponível.

## Banco e publicação

O patch V27 adiciona `IaTentativas` e `IaPoliticaVersao` a ConversasAtendimento.
Ele é idempotente e aplicado na inicialização por padrão, após o V26. Se os
patches estiverem desabilitados no ambiente, aplique as duas alterações de
DatabaseSchemaV27Initializer antes de iniciar esta versão. Não apaga mensagens.

Publique o pacote completo no Render. A automação é executada dentro do processo
do aplicativo: não depende de uma aba aberta, mas depende do servidor ativo.
Uma instância gratuita do Render pode suspender após 15 minutos sem tráfego de
entrada; durante a suspensão, a leitura do Gmail e a retomada não executam.
Ao reativar, as rotinas voltam a processar as pendências dentro dos seus limites.
Para operação contínua, a execução deve ficar em um serviço que permaneça ativo.

Referências do provedor:
[suspensão de instâncias gratuitas](https://render.com/docs/free) e
[RENDER_EXTERNAL_URL](https://render.com/docs/environment-variables).

## Validação realizada

- Compilação .NET 8 concluída sem erros. Os dois avisos preexistentes de
  ForgotPassword e CanalIcon permanecem no build completo.
- 225 verificações de backend aprovadas, incluindo geração, envio sem aprovação,
  continuidade, limite de tentativas, retomada, exclusão de mensagens antigas,
  preservação do atendente, controles de identidade e ausência de alterações na agenda.
- O cenário "Quero marcar uma consulta com o dr david" usa o agente e a agenda
  reais sobre banco de teste, com Gemini simulado. Busca uma vaga do Dr. David,
  monta o link preenchido e registra o envio sem criar uma consulta.
- O e-mail HTML preserva todos os parâmetros do link e escapa o conteúdo.
- Testes de navegador aprovados para resposta automática sem aprovação,
  progresso, conexão, formulários manuais e a organização compacta.

Nenhum e-mail real foi enviado durante os testes. A classificação semântica do
Gemini e a entrega Gmail/Brevo precisam ser verificadas após a publicação.

Para conferir em produção, use um remetente controlado e uma conversa em modo IA:
pergunte por médicos, solicite horários de um profissional e responda novamente.
Confira o envio automático, abra o link e confirme que ele exige login e abre
o formulário correto. Assuma a conversa e verifique que a automação pausa.
