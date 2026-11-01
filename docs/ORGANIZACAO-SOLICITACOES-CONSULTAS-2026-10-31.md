# Organização das solicitações e consultas

Data: 31/10/2026. Substitui a apresentação em uma única fila da revisão anterior.

## Como ficou a tela

A tela Solicitações e consultas tem duas abas de navegação. Cada aba exibe apenas seu conteúdo, com navegação por links e formulários GET, sem depender de JavaScript para trocar de aba ou filtrar.

### Solicitações

Mostra pedidos que ainda não têm consulta vinculada, em três etapas:

- Novos pedidos: conferir paciente, contato e especialidade. Reúne os estados Nova e EmTriagem.
- Definir horário: escolher uma vaga e concluir o agendamento. Corresponde a BuscandoHorario.
- Aguardando resposta: retomar o pedido quando o paciente responder. Corresponde a AguardandoPaciente.

A contagem da aba considera os pedidos em aberto. Ao concluir um agendamento e vincular a consulta, o atendimento aparece na aba Consultas. A solicitação original continua acessível pelo link Pedido na coluna Origem da consulta.

Sem pedidos em aberto, a tela mostra uma mensagem compacta e um atalho para Consultas, em vez de três grandes colunas vazias. Solicitações encerradas, canceladas ou com agendamento registrado sem consulta vinculada ficam no Histórico de solicitações. Os recursos de contingência permanecem nesta aba, dentro de Mais opções, recolhido por padrão. Ao expandir, ficam disponíveis os acessos Ver pedidos offline e Registrar sem conexão.

### Consultas

Mostra uma lista única com paciente, agendamento, situação, origem e ações. Os filtros disponíveis são:

- Todas: todas as consultas no resultado de busca e canal.
- A confirmar: consultas Pendente ou Remarcada de hoje em diante, com o mesmo critério usado pelo sino.
- Agendadas: consultas com estado Confirmada.
- Realizadas: consultas com estado Realizada.
- Outros registros: canceladas, ausências, estados adicionais e pendências com data anterior a hoje.

A lista mantém as ações Confirmar e Ver consulta e o acesso à central de confirmações. Registros com solicitação vinculada têm um link para o pedido original. Consultas sem solicitação continuam incluídas, sem atribuir um canal de origem que não foi registrado.

São exibidas até 20 consultas por página. Os contadores dos filtros consideram todo o resultado, e o rodapé informa a faixa exibida, o total e a navegação para as demais páginas. Páginas inválidas são limitadas ao intervalo disponível.

## Clareza e aparência

- Removidos da tela os blocos de indicadores que repetiam as contagens das listas.
- A aba Solicitações não mistura consultas confirmadas, realizadas ou pendentes de confirmação nas colunas de pedidos.
- Busca por paciente/contato e canal permanece disponível; os valores são mantidos nos links de abas, filtros e paginação.
- Filtrar a busca mantém a aba e o filtro de situação atuais, reiniciando a paginação.
- Consultas de datas anteriores ainda confirmadas recebem aviso para conferir o desfecho. A passagem da data não altera automaticamente seu estado.
- Mantidos a paleta, os temas claro e escuro, as bordas, os cartões e os botões clássicos.
- No celular, cada linha da lista vira um cartão com rótulos para os campos. Não há rolagem horizontal da página.
- Cache estático atualizado para v55.

## Verificações

- 15 verificações de lógica passaram em harness .NET 8 isolado. Incluem as regressões de pendências/realizadas sem solicitação, deduplicação, total do sino, separação completa entre abas, filtro de realizadas e paginação de 450 registros.
- Compilação Razor/C# em host isolado com Tag Helpers: cinco views de produção, incluindo a tela principal e suas duas parciais, além de duas views de suporte à prévia.
- 72 cenários de layout passaram: Admin e Funcionario, duas abas, dados completos/resultado vazio/apenas consultas, temas claro e escuro, larguras de 390, 768 e 1440 pixels.
- 16 cenários funcionais passaram: cinco filtros de situação, troca entre abas, persistência de busca/canal/situação e paginação, em celular e desktop. Os formulários de confirmação renderizados incluem token antifalsificação e URL de retorno.
- Conferidas a ausência dos antigos indicadores, a separação dos conteúdos no HTML, as contagens, a ausência de consultas duplicadas, a largura da página, a posição do título e a largura dos nomes no celular.
- CSS analisado sem erros e service worker verificado com node --check.

## Ajuste de contingência em Mais opções

O bloco de pedidos sem conexão passou a ficar dentro de um controle nativo details/summary chamado Mais opções. Ele começa fechado, abre por clique ou teclado e mantém os dois links originais. O funcionamento do registro offline, da senha, do armazenamento local e do envio de pedidos não foi alterado.

A view foi recompilada pelo Razor. Quatro verificações de interface passaram: celular/desktop e temas claro/escuro, conferindo menu inicialmente fechado, links corretos e visíveis após abrir, acionamento por Enter e ausência de estouro horizontal. CSS e sintaxe do service worker também foram conferidos.

## Limites

A validação usou dados fictícios e um host de prévia isolado. A aplicação completa, as consultas SQL no PostgreSQL, os logins reais e a suíte integral de testes não foram executados. Esta reorganização não altera o estado clínico das consultas, não cria registros durante a leitura e não exige migração de banco.
