# Retomada automática da IA no e-mail e WhatsApp

Data: 01/11/2026.

## Comportamento

Devolver para IA agora é uma única operação: reativa a IA, invalida a sessão antiga de atendimento e processa a última entrada ainda sem resposta. Não é necessário apertar outro botão depois da devolução.

- E-mail: a IA prepara um rascunho para revisão. Nenhum e-mail é enviado por essa retomada; Aprovar e enviar continua obrigatório.
- WhatsApp: a IA prepara a resposta e aciona o transporte configurado. As próximas mensagens continuam no fluxo automático do webhook enquanto a conversa estiver em modo IA.
- SMS e Site/PWA usam a mesma retomada conversacional.
- Sem entrada pendente: apenas informa que a IA está ativa. Não inventa uma mensagem, não reinsere a entrada e não repete uma resposta anterior.
- O botão Verificar e-mail pendente saiu da interface normal. A rota antiga permanece compatível para chamadas anteriores e reutiliza o processamento protegido contra duplicação.

A leitura de e-mails novos continua dependendo do leitor Gmail configurado e de seu intervalo de consulta. A devolução processa a mensagem que já está registrada na Central; não precisa reimportá-la do Gmail.

## Correções no e-mail

O código anterior já chamava a análise na devolução do e-mail, mas podia encaminhar a conversa imediatamente de volta à equipe. Foram corrigidas duas fontes de bloqueio indevido encontradas no código:

- Citações de respostas anteriores cujo cabeçalho Gmail está quebrado em várias linhas agora são separadas da mensagem atual. Também são tratados cabeçalhos em inglês, mensagens encaminhadas, cabeçalhos Outlook e linhas citadas.
- Um telefone de 11 dígitos não é automaticamente tratado como CPF. CPFs válidos ou explicitamente identificados continuam protegidos. Telefones são omitidos do texto antes do classificador e do gerador de rascunho, sem bloquear a dúvida administrativa.

A classificação continua semântica. O prompt distingue pedidos administrativos, como procurar um cardiologista e perguntar por horários, de operações privadas efetivas. Pedidos administrativos incompletos podem receber rascunhos de esclarecimento. Perguntas clínicas, acesso a dados individuais, identificação necessária ou pedido explícito de atendente continuam sendo encaminhados à equipe.

O contexto de e-mail não recebe o vínculo de paciente como prova de identidade. As ferramentas executadas nesse canal ficam limitadas a informações públicas de médicos, especialidades, disponibilidade e dados institucionais. O gerador de rascunho não pode cadastrar, identificar pacientes, ler consultas individuais ou alterar a agenda.

## Histórico, duplicação e falhas

- Avisos automáticos do sistema, como encaminhamento para equipe, não contam como resposta final à pergunta.
- Uma resposta posterior da equipe/IA ou um rascunho posterior já trata a entrada. Rascunhos descartados e saídas de transporte existentes não provocam geração repetida ao devolver a mesma conversa.
- A retomada é serializada por conversa no processo. Rascunhos e respostas também recebem uma chave vinculada à entrada, usando o índice único de envio já existente.
- A retomada não cria outra cópia da mensagem recebida.
- Respostas iguais para duas entradas diferentes continuam sendo respostas distintas. O reaproveitamento de avisos antigos por texto fica restrito a avisos do sistema.
- Falhas de geração e de transporte são apresentadas separadamente. Uma mensagem sem confirmação de envio não é anunciada como enviada.
- Um encaminhamento legítimo aparece como aviso, com o histórico preservado.
- A confirmação de envio de e-mail continua separada da geração do rascunho.

## Validação

- Compilados os 190 arquivos C# da aplicação com as dependências reais do projeto, usando o compilador do SDK .NET 8 diretamente.
- Geradas e compiladas as 83 views Razor contra a aplicação e os Tag Helpers reais.
- As 173 verificações executadas da suíte CallMed.Checks passaram com Entity Framework InMemory, clientes de IA simulados e transportes falsos.
- Os testes de retomada chamam inclusive o método POST DevolverParaIa do controller, verificando que um único acionamento prepara o rascunho do e-mail ou envia a resposta ao transporte WhatsApp simulado.
- Conferidos: mensagem atual sem citação antiga; preservação de texto novo após citação; tratamento de telefone e CPF; ausência de identidade implícita no e-mail; ferramentas privadas bloqueadas no rascunho; novos e-mails automáticos; novas mensagens WhatsApp automáticas; mensagens repetidas sem duplicação; duas retomadas simultâneas; falha de transporte; respeito a resposta humana e a pedido explícito de atendente; aviso de transferência sem bloquear a retomada.
- Dois fixtures antigos de testes foram ajustados aos construtores atuais dos serviços. A suíte inclui as regressões anteriores de agenda, identidade, envio e controle de repetição.
- Sintaxe de atendimento.js e do service worker conferida. Cache atualizado para v57.

## Limites

Os testes não utilizaram a conta Gmail, o modelo Gemini nem a conexão Evolution/WhatsApp de produção. Não foram enviados e-mails ou mensagens reais. A resposta semântica do modelo real e a entrega pelo provedor precisam ser verificadas no ambiente configurado do usuário. Os testes de persistência usam InMemory e inspeção do modelo PostgreSQL; a concorrência com múltiplas instâncias e o índice único não foram exercitados contra um PostgreSQL real.

A validação de compilação utilizou diretamente os compiladores C# e Razor do SDK, com os pacotes nas versões do projeto. Não corresponde a uma publicação ou implantação no servidor. Permaneceram avisos de compilação sobre referências de versão dos pacotes na compilação direta e avisos preexistentes de nulabilidade/função local não usada. Não foi necessária alteração de tabelas ou migration.
