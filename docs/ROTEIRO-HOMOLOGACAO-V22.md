# Homologação e piloto do desafio SENAI

## Aceitação funcional em banco de teste

Use dados fictícios e números de teste pertencentes à equipe. O resultado só deve ser marcado como aprovado depois de executado.

| Caso | Resultado esperado |
|---|---|
| Dois pacientes diferentes sem e-mail | Ambos salvos com Email NULL; nenhuma conta Identity criada automaticamente |
| E-mail inválido ou já usado por outro paciente | Cadastro rejeitado com mensagem compreensível |
| Remover e-mail de paciente com acesso digital | Edição rejeitada; login continua utilizável |
| Criar acesso público para CPF de cadastro sem e-mail | Solicita conferência na recepção; não vincula por CPF sozinho |
| Atualizar e-mail pela recepção e depois criar acesso | Conta associada ao paciente correto; histórico preservado |
| Criar pedido sem conexão e recarregar página | Pedido recuperado apenas com a senha da fila |
| Senha incorreta | Dados locais intactos, conteúdo não exibido |
| API negar acesso / sessão expirar | Fila mantida para nova tentativa autenticada |
| Reenviar após resposta perdida | Mesmo protocolo retorna a mesma solicitação; não cria duas linhas |
| Duas requisições simultâneas com mesmo protocolo | Um registro no PostgreSQL; índice e bloqueio impedem duplicidade |
| Duas abas modificando a fila | Alteração concorrente é detectada; pede reabertura, sem substituir pedidos da outra aba |
| Enviar pedido para triagem | Não há consulta nem reserva; cadastro, cobertura e vaga são validados pela equipe |
| Disputa pelo mesmo horário | Apenas um agendamento é aceito |
| Particular / convênio / especialidade | Comprovante mostra as orientações correspondentes |
| Cadastrar exame / paciente tentar acessar outro histórico | Registro aparece para o paciente certo; acesso indevido negado |
| Médico tentar ver paciente sem vínculo de consulta | Acesso negado |
| Reiniciar aplicação sobre o mesmo banco | Patch V22 pode ser reaplicado sem apagar dados |

## Testes de integração reais

1. Evolution: verificar estado na tela de integrações. De um número de teste autorizado, mandar mensagem para o número da clínica; observar conversa na central; responder pela equipe; confirmar recebimento. Executar o agendamento com confirmação explícita e conferir a consulta no banco/tela.
2. Repetir o mesmo webhook com o mesmo ID de mensagem e confirmar que não duplica o atendimento nem a consulta. Chave de webhook incorreta deve retornar 401; evento de grupo deve ser ignorado conforme o parser existente.
3. Lembretes: criar consultas fictícias dentro das janelas de 20–26 horas e 1–3 horas; manter o processo ativo até o ciclo do worker (30 minutos); observar envio, orientações e registro. Não afirmar entrega ao aparelho apenas porque o gateway aceitou a requisição.
4. Canal telefone: confirmar criação da tarefa para a equipe ligar. Registrar conclusão pela recepção. Não é robô de ligação.
5. SMTP e SMS: repetir com os provedores contratados e testar falhas, timeout e contato sem canal disponível.
6. HIS: com a documentação do fornecedor, validar os campos e IDs com o contrato em `integracoes/INTEGRACAO-LEGADO.md`. A API existente consulta cadastro por CPF, busca disponibilidade e cria solicitação para triagem. Ela não sincroniza automaticamente todas as agendas, não importa laudos e não implementa por si só o protocolo particular de qualquer HIS.
7. Anotar ambiente, versão da Evolution, data, responsável e resultado. Nunca colar chaves ou dados reais nas evidências do trabalho escolar.

## Usabilidade com o público

Convidar pacientes representativos (incluindo idosos e pessoas com pouca familiaridade digital) e profissionais da recepção. Explicar a tarefa e deixar a pessoa tentar antes de ajudar. Usar dados fictícios e respeitar a decisão de interromper o teste.

Tarefas: solicitar consulta, localizar data/horário, confirmar, remarcar, cancelar, aumentar texto e pedir ajuda por telefone. Recepção: registrar paciente sem e-mail, triar convênio, oferecer vaga e reconciliar pedido offline.

Registrar por tarefa: conclusão sem ajuda (sim/não), tempo, número de erros, ajuda necessária e comentário do participante. Corrigir primeiro bloqueios de navegação, rótulos não compreendidos e ações difíceis de tocar. Testar teclado e leitor de tela além dos botões de acessibilidade.

## Indicadores antes e depois

Definir dois períodos comparáveis, com a mesma unidade e regras de contagem. Registrar quantidade observada junto de cada resultado; não estabelecer uma melhoria fictícia.

| Indicador | Cálculo / coleta |
|---|---|
| Tempo de confirmação | Confirmação da solicitação menos solicitação inicial; captura offline já preserva o tempo original |
| Absenteísmo | Ausentes / (realizadas + ausentes) × 100; marcar os desfechos de todas as consultas concluídas |
| Retrabalho | Solicitações que exigiram correção / solicitações concluídas × 100; registrar motivo durante o piloto |
| Autonomia | Participantes que concluíram sem ajuda / participantes que tentaram × 100 |
| Carga da recepção | Minutos de trabalho por atendimento e quantidade de contatos necessários |
| Uso de papel | Folhas utilizadas por período e volume de atendimentos |

O relatório atual usa o mês corrente e o tempo médio de solicitações já confirmadas. Não trata solicitações ainda pendentes como tempo zero. Para comparar períodos, guardar a apuração ao fim de cada período; o projeto não oferece um experimento de impacto causal automático.

## Orçamento a preencher com a empresa

| Item | Quantidade / consumo | Custo mensal cotado | Fonte e data |
|---|---|---|---|
| Hospedagem CallMed + Evolution | Servidor, memória e disponibilidade | A preencher | A preencher |
| Banco do CallMed | Plano Neon ou PostgreSQL existente | A preencher | A preencher |
| Banco e Redis da Evolution | Inclusos no servidor ou separados | A preencher | A preencher |
| SMS | Mensagens mensais × preço por envio | A preencher | A preencher |
| E-mail | Provedor e volume | A preencher | A preencher |
| IA | Chamadas e consumo do modelo | A preencher | A preencher |
| Domínio, backups e suporte | Custo distribuído por mês | A preencher | A preencher |

Total mensal = soma dos custos. Custo por agendamento = total / consultas agendadas no período. Software disponível sem licença paga não significa operação sem custo. Comparar capacidade e custo com a infraestrutura já existente antes da contratação.

## Ficha de contingência presencial

Usar somente quando a fila digital não estiver disponível. Campos mínimos: protocolo manual, data/hora da solicitação, nome do contato, telefone opcional, especialidade desejada, preferência de data/período, canal e responsável. Não registrar laudos nem prometer um horário. Guardar a ficha com a recepção. Ao retornar a conexão, digitar em **Solicitações → Nova solicitação**, anotar o protocolo do sistema na ficha e marcar como transcrita para evitar duplicação. Aplicar o procedimento de guarda/descarte definido pela clínica.
