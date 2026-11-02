# Alertas e confirmações com o visual do CallMed

Data: 31/10/2026.

## Apresentação

As confirmações da aplicação passam a usar diálogos próprios do CallMed, com ícone, título, descrição, botão de voltar, ação explícita e fechamento por X ou Esc. Cancelamentos, exclusões e outras ações destrutivas recebem destaque vermelho no botão de confirmação. O foco inicial fica em Voltar, e a navegação por Tab permanece dentro da janela.

Mensagens de sucesso, erro, atenção e informação usam cores e ícones correspondentes. Os alertas nas páginas, avisos e resumos de validação recebem o mesmo acabamento. Alertas transitórios podem ser fechados; as mensagens de validação continuam junto ao formulário. Não há descarte automático de mensagens por tempo.

O CSS continua na folha única callmed.css, com suporte aos temas claro e escuro e adaptação para celular. Os textos das mensagens são inseridos como texto, sem interpretação de HTML.

## Ações preservadas

- As antigas chamadas alert/confirm da aplicação foram substituídas pela API CallMedUI em feedback.js.
- Dez formulários com confirmação inline usam atributos declarativos. Ações com data-confirm, inclusive rascunhos de e-mail criados dinamicamente, são tratadas pelo mesmo componente.
- Antes da decisão, o envio é interrompido. Voltar, X, Esc e clique fora não enviam o formulário.
- Ao confirmar, requestSubmit reapresenta o formulário com o botão original. Isso preserva a validação do navegador, os campos ocultos, o token antifalsificação e o nome/valor do botão acionado.
- Uma aprovação é consumida uma única vez. Pedidos de envio repetidos durante a confirmação ou o processamento não criam outro envio.
- Os indicadores de processamento da Central de Atendimento só são aplicados depois da aprovação. Os botões mantêm seu conteúdo no POST.
- A limpeza de mensagens do assistente e a exclusão de pedidos locais aguardam a resposta do diálogo antes de alterar os dados.
- O controle de permissão de notificações continua sendo apresentado pelo navegador. As mensagens da aplicação sobre essa permissão usam o novo componente.

## Carregamento

feedback.js é carregado pelo cabeçalho compartilhado das telas autenticadas, públicas e de acesso. O formulário estático de contingência também carrega o componente. O service worker inclui o novo arquivo entre os recursos da interface; o cache foi atualizado para v56.

## Verificações executadas

- 36 cenários visuais: informação, sucesso, atenção, erro, confirmação comum e ação destrutiva; larguras de 390, 768 e 1440 pixels; temas claro e escuro.
- 10 grupos de verificações funcionais passaram: cancelamento e retorno do foco; fechamento por Esc/X/fundo; um único POST com ID, botão e token preservados; consumo único da aprovação; aprovação de rascunhos e encerramento de conversas; validação obrigatória antes e depois da confirmação; mensagens tratadas como texto; fila de diálogos; mensagens de validação dinâmicas; fechamento de alertas inline.
- Nenhum erro JavaScript apareceu nesses testes de navegador. As janelas permaneceram dentro da tela e o foco por Tab permaneceu dentro do diálogo.
- O formulário de contingência foi aberto em um navegador de teste com a rede desativada após preparação do cache. O componente foi carregado do cache; cancelar a exclusão preservou um pedido fictício, e confirmar removeu somente esse pedido local.
- As dez views de produção alteradas passaram pela geração Razor. O host de prévia compilou nove views, incluindo o cabeçalho compartilhado e as views de apoio aos testes.
- Sintaxe dos 15 scripts JavaScript e do service worker conferida. Análise CSS sem erros. Nenhuma chamada nativa alert/confirm permaneceu nas views ou scripts da aplicação.
- Controllers, entidades, serviços, acesso a dados e migrations foram comparados com o pacote anterior e permaneceram iguais.

## Limites

A validação usou um host ASP.NET isolado, dados fictícios e captura dos POSTs no navegador. Nenhuma consulta real foi cancelada, nenhum e-mail foi enviado e nenhum dado de produção foi alterado. A compilação integral do projeto e os fluxos contra o banco real não foram executados. O teste offline descreve apenas o ambiente de teste preparado; não certifica que um dispositivo de produção já possui os arquivos em cache.
