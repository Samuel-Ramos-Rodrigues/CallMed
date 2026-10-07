# CallMed — controle de envios pela Evolution

Atualização de 27/09/2026, aplicada sobre o pacote `CallMed-CSS-Revisado.zip`.

## Problema identificado

A tela apresentada mostrava dois avisos da mesma vaga com erro HTTP 429. Esse código indica limitação de requisições pelo servidor. O print não identifica a cota, o responsável pela hospedagem ou o prazo real de liberação.

No código havia três pontos relacionados: nenhuma espera após 429; o reenvio manual criava outro registro; a lista de espera criava outro aviso a cada processamento sem sucesso. O mesmo padrão existia nos lembretes de consulta.

## Comportamento corrigido

- Os clientes da Evolution compartilham uma fila de envio dentro da aplicação, com intervalo mínimo configurável.
- Em 429, o envio respeita `Retry-After` em segundos ou data HTTP. Sem prazo válido, aplica espera progressiva, partindo de 60 segundos por padrão.
- Durante essa pausa, outras conversas do mesmo processo também deixam de chamar o envio da Evolution.
- O reenvio manual atualiza a mensagem existente. Cliques concorrentes disputam uma versão persistida no banco; somente a tentativa que reservou a mensagem pode chamar o provedor.
- Lista de espera e lembretes usam uma chave única por evento. Novas execuções consultam o mesmo aviso. Um aviso equivalente da versão anterior pode ser reaproveitado; registros históricos não são apagados.
- Uma notificação só é marcada como concluída depois de um resultado de sucesso. Uma falha 429 fica disponível para nova tentativa depois do prazo, no próximo ciclo do worker existente.
- O prazo de cada mensagem é salvo no banco. A interface mostra a espera, desabilita o botão durante o intervalo e atualiza o estado sem duplicar a mensagem.
- Falhas sem confirmação do resultado e envios parcialmente confirmados bloqueiam a repetição integral: a equipe deve conferir o canal e, se necessário, enviar somente o conteúdo faltante.
- Erros de configuração, como 401, não são repetidos automaticamente. Depois de corrigir a configuração, a equipe pode usar o reenvio manual.

## Atualização do banco

Esta versão acrescenta quatro colunas em `MensagensAtendimento`: `ChaveEnvio`, `ProximaTentativaEm`, `ReenvioBloqueado` e `VersaoEnvio`, além de um índice único para avisos.

O projeto já utiliza patches de esquema na inicialização. O novo `DatabaseSchemaV23Initializer` segue esse mecanismo e está registrado depois do V22. A configuração `Database:ApplyV23Patch` está habilitada por padrão. A conta do banco usada pela aplicação precisa poder acrescentar as colunas e o índice.

Se os patches automáticos estiverem desativados na hospedagem, aplique [ATUALIZACAO-ENVIO-V23.sql](integracoes/ATUALIZACAO-ENVIO-V23.sql) antes de iniciar esta versão. O script é aditivo e pode ser executado novamente; não remove dados. As migrations e os patches anteriores continuam necessários em instalações novas, conforme o README.

## Configuração

| Configuração | Padrão | Uso |
|---|---:|---|
| `Atendimento:WhatsApp:Evolution:IntervaloMinimoEnvioMs` | 2000 | Intervalo mínimo entre chamadas de envio no mesmo processo |
| `Atendimento:WhatsApp:Evolution:EsperaApos429Segundos` | 60 | Espera inicial quando não há Retry-After válido |
| `Database:ApplyV23Patch` | true | Aplicar as colunas e o índice na inicialização |

Em variáveis de ambiente, substitua `:` por `__`. Esses valores são controles do CallMed, não declarações de uma cota oficial da Evolution. Ajuste o intervalo conforme o limite do seu provedor.

A pausa compartilhada entre conversas é mantida na memória de cada processo. A espera de cada mensagem e a reserva de envio ficam no banco. Se houver várias réplicas ou outros sistemas usando a mesma instância Evolution, o limite agregado deve ser coordenado na infraestrutura; esta alteração não controla os envios desses outros processos.

Os workers mantêm seus intervalos existentes: lista de espera a cada 15 minutos e lembretes a cada 30 minutos. O prazo informado libera uma tentativa futura; não garante que o provedor já tenha liberado a cota. Se o processo terminar durante um envio sem gravar o resultado, a mensagem permanece em processamento para evitar uma repetição automática incerta; confira o WhatsApp antes de enviar novamente.

## Verificações

- Compilação Release: zero avisos e zero erros.
- 44 verificações de código aprovadas: 24 anteriores e 20 relacionadas ao envio, limites, concorrência, persistência e lista de espera.
- HTTP 429, Retry-After numérico e por data, ausência de cabeçalho, espera progressiva, compartilhamento entre destinatários, compatibilidade de payload, envio parcial e transporte incerto exercitados com respostas simuladas.
- Reenvio do mesmo registro, reaproveitamento de aviso antigo, bloqueio de POST durante espera, disputas entre requisições e processamento completo da lista de espera exercitados com EF Core InMemory.
- Gerado o SQL de criação do modelo PostgreSQL para verificar o mapeamento dos novos campos e índice. O patch não foi executado no banco real.
- Central de atendimento verificada em 1440 e 390 px, temas claro e escuro: espera, botão, atualização por polling, POST com antiforgery, ausência de duplicação, erros JavaScript e transbordamento horizontal.

Registros em `docs/validacao/whatsapp/`. Os testes não fizeram chamadas a um WhatsApp real, nem enviaram mensagens ou alteraram seu ambiente publicado.

```sh
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release --no-restore
dotnet run --project tests/CallMed.Checks -c Release
```

## Se o HTTP 429 continuar

Consulte o limite de requisições/mensagens da hospedagem da Evolution, o consumo da instância e o cabeçalho `Retry-After` da resposta. Outros aplicativos conectados à mesma instância também podem consumir essa cota. O código agora reduz repetições desnecessárias e respeita a espera; ele não remove um bloqueio imposto pelo provedor.

Referências do protocolo: [HTTP 429 — RFC 6585, seção 4](https://www.rfc-editor.org/rfc/rfc6585.html#section-4) e [Retry-After — RFC 9110, seção 10.2.3](https://www.rfc-editor.org/rfc/rfc9110.html#section-10.2.3).
