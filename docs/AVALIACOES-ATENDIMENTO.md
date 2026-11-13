# Avaliação ao final do atendimento — 04/11/2026

## Comportamento

- WhatsApp, SMS e e-mail: ao encerrar, o CallMed solicita uma nota de **1 a 5**. O paciente responde `5` ou `NOTA 5 - comentário opcional` pelo próprio canal. A escala vai de muito ruim a muito bom.
- Site: o agente autenticado oferece **Encerrar e avaliar**, com cinco notas, comentário opcional de até 1.000 caracteres, **Enviar avaliação** e **Agora não**. Fechar/minimizar a janela apenas oculta o widget.
- A pesquisa abre quando a equipe usa **Encerrar**, quando o paciente usa o botão do site ou envia uma despedida explícita (`pode encerrar`, `era só isso`, `tchau`, `até mais`). Um agradecimento isolado não encerra o fluxo de agendamento. Não há encerramento automático por inatividade.
- A resposta à pesquisa fica em tabela própria e **não reabre o atendimento nem entra no contexto da IA**. Uma nova pergunta inicia a continuidade normalmente. Números enviados enquanto a conversa está ativa continuam disponíveis para selecionar opções de agenda.
- A avaliação é opcional, expira após sete dias e aceita uma única resposta por encerramento. Nos canais, `PULAR AVALIAÇÃO` ou `NÃO AVALIAR` dispensa a pesquisa. Para comentar, envie nota e comentário na mesma mensagem.
- Uma nova conversa pode ser encerrada e avaliada novamente. Apenas a pesquisa mais recente fica pendente; pesquisas anteriores respondidas permanecem no relatório.
- Presencial e telefone não recebem pesquisa por um canal digital diferente automaticamente.

## Acompanhamento

A equipe acessa **Central de Atendimento → Avaliações**, ou **Relatório geral → Avaliações do atendimento**. A página `/Avaliacoes` exige conta de funcionário/administrador com cadastro ativo.

Há filtros por mês de encerramento e canal, quantidade respondida, média de 1 a 5, satisfação (percentual de notas 4 ou 5 entre as respondidas), resumo por canal e comentários com acesso à conversa. Sem resposta não conta como nota zero. O total de avaliações abertas inclui pesquisas ainda não enviadas ou dispensadas; ele não afirma entrega pelo provedor. As datas são exibidas no horário da clínica.

## Publicação e banco

1. Atualize o código no serviço **CallMed** do Render e faça o deploy.
2. Na inicialização, o patch aditivo **V28** cria `AvaliacoesAtendimento` e seus índices. `Database:ApplyV28Patch` é `true` por padrão. Não altera tabelas dos provedores, agendamentos nem mensagens existentes.
3. Não são necessárias novas chaves nem variáveis dos canais. Evolution, Gmail/SMTP e httpSMS mantêm a configuração atual.
4. Para manutenção manual do banco, o mesmo SQL está em `docs/sql/V28-AvaliacoesAtendimento.sql`. O usuário do banco precisa poder criar tabela e índices, como nos patches anteriores.
5. O cache estático foi atualizado para carregar o novo cartão do site.

## Persistência e falhas

Encerramento e pesquisa são confirmados na mesma transação PostgreSQL. A chave única `(ConversaAtendimentoId, UltimaEntradaId)` evita abrir a mesma pesquisa duas vezes; a primeira resposta válida é preservada por controle de concorrência. A identidade da última entrada é um snapshot sem vínculo obrigatório ao histórico que pode ser aparado.

Um serviço em segundo plano verifica convites e agradecimentos persistidos a cada cinco segundos. O webhook de SMS não aguarda envio HTTP nem IA. As mensagens usam chaves idempotentes `feedback:*`, aproveitando a proteção de envio existente. Falhas aparecem no histórico da Central, com o reenvio existente; mensagens com resultado incerto mantêm o bloqueio de repetição. Reiniciar o processo preserva a fila de avaliações. Se outra conversa já começou, o convite antigo é suprimido para não interrompê-la.

A nota e o comentário são validados no servidor. O site exige autenticação, cadastro ativo, antiforgery e propriedade da conversa. Os comentários do relatório são texto codificado pelo Razor; não são interpretados como HTML nem enviados ao modelo de IA.

## Verificação

- Suíte `.NET 8`: `dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj`.
- Widget com CSS/JS reais e endpoints locais simulados: `npm --prefix tests install` e `npm --prefix tests run test:feedback` (instale o Chromium do Playwright ou informe `CHROME_PATH`). `CALLMED_SCREENSHOTS` é um diretório opcional para capturas.
- Homologação após o deploy: envie uma mensagem ao canal, encerre na Central, responda à pesquisa e confira `/Avaliacoes`. No site, use **Encerrar e avaliar**, envie uma nota e confira o mesmo relatório. A homologação usa a configuração real dos provedores; os testes automatizados não enviam mensagens reais.
