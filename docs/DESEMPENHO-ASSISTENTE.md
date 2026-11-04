# Correção da espera no assistente — 04/11/2026

## O que foi encontrado no código

- Até cumprimentos isolados, como `Oi`, aguardavam o modelo remoto.
- A avaliação consultava a conversa no banco antes de toda mensagem comum do widget. O polling também resolvia o mesmo paciente duas vezes.
- A resposta só chegava ao navegador depois de gravar o mesmo par de mensagens no histórico V12 e na Central. Na primeira interação de uma sessão, esse trecho fazia quatro `SaveChangesAsync`, além das consultas e da preparação da conversa.
- O navegador repetia o POST inteiro após HTTP 502/503/504 ou falha de rede. Uma requisição lenta podia ser seguida por outra espera completa.
- O timeout do Gemini era por chamada; várias rodadas de ferramentas não tinham um orçamento único para o atendimento do site.

Esses pontos foram verificados no código. Sem o log da requisição lenta no Render, não foi medida a contribuição do banco, da infraestrutura ou do Gemini no episódio mostrado no print.

## Alterações

1. Cumprimentos isolados (`Oi`, `Olá`, `Bom dia`, `Boa tarde`, `Boa noite`) recebem resposta local no serviço do agente. Uma frase como `Oi, quero marcar uma consulta` continua no fluxo completo. Autorização, sessão, registro da conversa e confirmação de ações permanecem ativos.
2. Uma mensagem comum do widget passa pelo filtro de feedback sem consultar o banco. Notas, dispensas e despedidas continuam no fluxo persistido de avaliações.
3. O polling autentica e resolve o paciente uma única vez.
4. Entrada e resposta do site são gravadas juntas na Central, aparando o histórico uma vez. O teste comparativo do mesmo par caiu de quatro gravações para uma. A leitura dos históricos V12 antigos continua disponível; eles não são apagados.
5. A ação `/Agente/Enviar` tem orçamento total padrão de **40 segundos**, incluindo preparação no banco, espera da sessão e geração. Ao excedê-lo, cancela o processamento e responde HTTP 504 com uma mensagem que libera o chat.
6. O navegador faz um único POST e limita a própria espera a **55 segundos**. Não repete automaticamente um pedido que pode ter sido processado. Após oito segundos, a bolha informa que a resposta está demorando mais que o normal.
7. O cache estático foi atualizado para carregar o novo JavaScript.

As integrações Evolution, httpSMS e Gmail/SMTP e a lógica das ferramentas de agendamento foram preservadas. Os três ajustes do ZIP `CallMed-TWA-AssetLinks-Projeto-Completo.zip` também foram mantidos nesta entrega.

## Publicação

Publique o código atualizado no serviço CallMed do Render. Não há novo patch de banco, chave ou variável obrigatória.

O valor padrão já está no projeto:

```env
Gemini__InteractiveTimeoutSeconds=40
```

Essa opção limita a ação do chat do site; `Gemini__TimeoutSeconds` continua sendo o timeout HTTP da integração. O limite do navegador permanece em 55 segundos.

## Diagnóstico no Render

Os logs passaram a registrar, sem a mensagem do paciente:

- `Chat pronto para o agente`: `PreparacaoMs` e `TraceId`.
- `Gemini concluído`: modelo, tentativa e `DuracaoMs` da chamada que respondeu.
- `Chat finalizado`: `TotalMs` e `TraceId`.
- `Chat excedeu o limite`: limite configurado e `TraceId`.

Eles permitem distinguir preparação no banco, tempo do modelo e tempo total durante a homologação. A redução verificada nos testes é de trabalho executado; não é uma promessa de tempo de resposta do provedor em produção.

## Verificações

- `dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj`: regressões dos canais e avaliações, saudação sem HTTP, frase com pedido indo ao modelo, gravação em lote, cancelamento da sessão e resposta após timeout. As chamadas externas são simuladas.
- `npm --prefix tests run test:feedback`: cartão de avaliação, temas/telas, HTTP 504 sem repetição de POST, aviso de demora e recuperação depois do limite do navegador. Os temporizadores são acelerados nos cenários de timeout do teste.
- `dotnet publish CallMedCrud/CallMedCrud.csproj -c Release`: compilação de publicação com as páginas Razor.

Depois do deploy, teste dois cumprimentos seguidos e um pedido real de consulta. Se um pedido demorar, use o mesmo `TraceId` nos logs de preparação e conclusão para identificar a etapa.
