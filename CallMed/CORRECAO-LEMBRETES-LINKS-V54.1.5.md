# CallMed NEXT V54.1.5 — Links de lembretes

## Problemas identificados na V54.1.4

1. `ConsultaLinkService` montava o endereço de confirmação a partir de `RENDER_EXTERNAL_URL` ou `Twa:Origin`, em vez de usar a origem pública canônica `PublicOrigin`. Isso podia criar links para o serviço Render errado, especialmente em execuções do worker separado.
2. O e-mail SMTP só transformava os endereços `/Consulta` e `/Consulta/Create` em links HTML clicáveis. O endereço `/consulta-acao?token=...` do lembrete ficava como texto simples no HTML.
3. A mensagem dizia que haveria um link mesmo quando a geração retornava `null`.
4. Links usados, alterados ou expirados retornavam uma resposta HTTP 410 sem página explicativa.
5. Os links de preparação, ofertas da lista de espera e transferência do Android para o portal usavam combinações diferentes de variáveis de origem.

## Correções

- `PublicOrigin` passa a ser priorizada para URLs externas; aliases antigos continuam válidos se a configuração explícita não existir. Origem explícita inválida não provoca fallback para domínio diferente.
- O SMTP transforma URLs HTTPS do domínio oficial em `<a href="...">` com escape HTML. Não transforma destinos de terceiros arbitrários em botões clicáveis.
- O lembrete informa o link só quando a URL é válida. Sem URL, orienta o paciente a entrar pela conta.
- Foi criada uma página pública amigável para link expirado/usado/alterado, com código HTTP 410, sem revelar informações clínicas.
- Preparação, lista de espera e transferência segura do aplicativo utilizam a mesma resolução de origem.
- O token continua protegido com `CallMed.ConsultaLink.v1` e mantivemos o formato dos links antigos. Nenhuma verificação de identidade ou proteção contra reutilização foi removida.

## Configuração obrigatória no Render

No serviço **web** e, se houver, no **worker** externo:

```text
PublicOrigin=https://callmed.onrender.com
```

Use o domínio HTTPS oficial real caso o portal tenha mudado. Não acrescente `/`, caminho de rota, parâmetros nem barra ao final.

O site e o worker devem conectar ao mesmo PostgreSQL e usar o mesmo material persistido de Data Protection, inclusive certificado de proteção, quando configurado. **Não altere ou reemita a chave/certificado sem planejar a preservação das chaves antigas**, pois isso pode invalidar links enviados e sessões.

Não configure `PublicOrigin` com a URL da API da Evolution, o endpoint do httpSMS ou o hostname interno do worker.

## Como testar sem mexer em pacientes reais

1. Gerar consulta futura para um paciente fictício com canal de teste de e-mail, WhatsApp ou SMS.
2. Testar um lembrete novo e confirmar que o texto inclui `https://callmed.onrender.com/consulta-acao?token=...`.
3. No e-mail, conferir que o link é clicável (âncora HTML), não apenas texto.
4. Abrir a URL em aba anônima: a tela **Gerenciar minha consulta** deve abrir. Abrir não confirma nem cancela automaticamente.
5. Executar confirmação **somente** na consulta fictícia; a segunda tentativa do mesmo link deve apresentar a nova tela de indisponibilidade (HTTP 410).
6. Testar URL adulterada ou expirada: a página deve orientar a entrar na conta, sem mostrar dados da consulta.
7. Testar os links de preparação clínica e de lista de espera com as configurações de origem do worker.
8. Executar as verificações de código: `dotnet run --project tests/CallMed.Checks -c Release -- --lembretes-links`.

**Importante:** mensagens já enviadas não mudam após o deploy. Um link antigo também pode estar expirado, já ter sido consumido ou ter sido gerado com o domínio errado. Para verificar a correção, gere **um novo lembrete de teste**.

## Limites da validação desta entrega

Os contratos de código foram verificados estaticamente. Este ambiente não dispõe do SDK .NET, do banco PostgreSQL nem de uma execução de envio com os canais conectados; portanto, a compilação .NET e o clique real em uma mensagem enviada ainda precisam ser testados após o deploy. O APK não foi recompilado.
