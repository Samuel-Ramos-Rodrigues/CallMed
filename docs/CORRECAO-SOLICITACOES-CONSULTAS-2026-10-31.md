# Correção de solicitações, confirmações e consultas realizadas

Data: 31/10/2026.

Atualização de apresentação: a tela foi posteriormente separada em duas abas. Consulte ORGANIZACAO-SOLICITACOES-CONSULTAS-2026-10-31.md para a organização atual. As correções de dados descritas neste registro foram preservadas.

## Problema corrigido

O sino carregava consultas pendentes diretamente de Consultas, mas o painel carregava apenas SolicitacoesAtendimento. Assim, consultas antigas ou diretas sem solicitação vinculada não apareciam. Além disso, marcar uma consulta como realizada encerrava sua solicitação; a tela retirava esse pedido das quatro colunas e deixava o atendimento concluído em um histórico recolhido.

## Comportamento atualizado

- O painel reúne solicitações e consultas diretas, preservando os vínculos e exibindo cada consulta uma única vez.
- A área Aguardando confirmação mostra consultas pendentes e remarcadas de hoje em diante, com os mesmos estados e data usados pelo sino. O formulário permite confirmar a consulta e voltar à mesma página com os filtros preservados.
- Consultas realizadas têm contador e área próprios, sempre visíveis. O estado da consulta prevalece sobre o estado histórico de sua solicitação, inclusive quando esta já foi encerrada.
- As quatro etapas originais continuam disponíveis. A coluna de agendadas mostra consultas confirmadas; as pendentes ficam em Aguardando confirmação e as realizadas em Consultas realizadas.
- Cancelamentos, ausências e pendências com data anterior a hoje permanecem no histórico, com links para suas consultas.
- A leitura do painel não insere nem modifica registros. Não são necessárias novas migrações.
- Busca por paciente, CPF, telefone e e-mail funciona nos dois tipos de registro. Os filtros afetam os contadores e todas as áreas da página. Sem filtros, o painel não aplica o antigo corte global de 400 registros.
- Uma consulta sem solicitação não tem seu canal inferido: aparece como Canal não informado e pode ser localizada por esse filtro.
- Para administrador e atendente, o total do sino é contado no banco e independe da quantidade de itens da prévia. Solicitações que já têm consulta vinculada não são contadas novamente como pedidos pendentes. O sino também pode incluir pedidos ainda sem agendamento; estes aparecem nas etapas da fila.

## Ajustes visuais

- Removido o conflito de especificidade que criava uma segunda borda no campo de busca.
- Estilos dos botões do menu da conta deixam de sobrepor os botões de confirmação.
- Bandeja do sino posicionada dentro da largura do celular, com altura limitada e rolagem.
- Altura do cabeçalho medida para que a navegação por âncoras mantenha o destino visível.
- Cache estático atualizado para v53.

## Validação executada

- 12 verificações específicas passaram: três pendências sem solicitação, realizadas vinculadas e diretas, estado atual após remarcação, ausência de duplicações, preservação das etapas, classificação de históricos, cobertura integral dos registros, igualdade das consultas pendentes do sino e painel, canal desconhecido, dois pedidos vinculados à mesma consulta, mais de 400 consultas e total do sino independente da prévia.
- Essas verificações foram compiladas e executadas em .NET 8 num harness isolado com as entidades Consulta e SolicitacaoAtendimento e os modelos reais do painel. Os cadastros relacionados foram representados por tipos mínimos de teste. O arquivo de regressão também integra tests/CallMed.Checks.
- As quatro views de produção alteradas foram geradas pelo compilador Razor MVC e compiladas em C# com Tag Helpers. A página de solicitações, seu cartão e a bandeja foram renderizados em um host ASP.NET isolado, com dados fictícios e cabeçalho extraído do layout atual.
- 72 cenários visuais passaram: Admin e Funcionario; dados presentes, resultado vazio e resultado vazio com filtro; larguras 390, 600, 760, 768, 1440 e 1712 pixels; temas claro e escuro.
- Verificados: largura da página e da bandeja, título abaixo do cabeçalho na abertura, destino das âncoras, três confirmações, duas realizadas, ausência de cartões duplicados, formulários com token antifalsificação e campo de busca sem borda interna.
- Análise de sintaxe CSS sem erros. Os 14 arquivos JavaScript e o service worker passaram em node --check.

## Limites da validação

Não houve acesso ao banco de produção, a logins reais ou à configuração do ambiente do usuário. A aplicação completa, as consultas SQL contra PostgreSQL e a suíte completa CallMed.Checks não foram executadas. Os testes de lógica e de Razor descritos acima são isolados; não substituem a homologação do projeto completo com seu banco.
