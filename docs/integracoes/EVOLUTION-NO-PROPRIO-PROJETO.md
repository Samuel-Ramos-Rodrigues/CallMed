# Evolution API junto do CallMed

Sim: o CallMed pode integrar e hospedar a Evolution na mesma infraestrutura. O CallMed continua como aplicação C#/ASP.NET Core; a Evolution roda como outro serviço, comunicando-se por HTTP e webhook. Não é uma biblioteca C# para colar no Program.cs. O agente do CallMed já processa as mensagens, sem exigir n8n.

## Arquitetura preparada

| Componente | Papel |
|---|---|
| CallMed | Pacientes, solicitações, triagem, agenda, agente e central |
| Evolution | Conexão WhatsApp, envio HTTP e eventos de entrada |
| PostgreSQL/Neon CallMed | Dados do sistema existentes |
| PostgreSQL Evolution | Persistência separada da integração |
| Redis e volume de instâncias | Estado/cache e sessão da Evolution |

O arquivo `compose.evolution.yml` sobe CallMed, Evolution, PostgreSQL da Evolution e Redis no mesmo projeto Compose. Mantém a conexão do CallMed que você informar, incluindo Neon. O banco da Evolution é separado para não misturar seus schemas com os do CRUD.

## Teste local em Windows com Docker Desktop

1. Na raiz do projeto, copie `.env.evolution.example` para `.env.evolution`.
2. Preencha a conexão do CallMed, `EVOLUTION_API_KEY`, `CALLMED_WEBHOOK_SECRET` e `EVOLUTION_DB_PASSWORD`. Use valores aleatórios diferentes; senha hexadecimal do banco evita caracteres especiais na URI. Preencha `GEMINI_API_KEY` para o agente.
3. Mantenha o nome de instância `callmed` ou use o mesmo nome em todos os lugares. A imagem está fixada como exemplo em `evoapicloud/evolution-api:v2.3.7`; homologue a versão escolhida com seu número. Não foi executado download/boot dessa imagem nesta entrega.
4. Se o banco é novo, habilite migrations e configure o administrador inicial. Se é o banco existente, preserve sua política atual e mantenha os patches habilitados.
5. Execute na raiz:

```powershell
docker compose --env-file .env.evolution -f compose.evolution.yml up -d --build
```

Abra `http://localhost:10000` para o CallMed. Evolution fica em `http://localhost:8080`. O exemplo usa Development e portas publicadas somente no loopback para teste local; essa não é uma publicação de produção.

## Conectar o número e configurar o webhook

O script pede as chaves de modo oculto, consulta a instância, cria se o servidor informar 404, configura o webhook e tenta gerar uma página local com QR Code. Não envia mensagens de teste por conta própria.

```powershell
powershell -File .\scripts\configurar-evolution.ps1
```

Se sua versão exige corpo direto em `/webhook/set`, execute com `-WebhookFormat flat`. O formato padrão `v2` usa o envelope `webhook`. Erros de autenticação/conectividade precisam ser corrigidos antes de prosseguir.

Abra `qr-evolution.html`, se gerado, e conecte pelo menu **Aparelhos conectados** do WhatsApp. Exclua o arquivo após a conexão. Se sua instalação usar um painel/Manager separado, você também pode criar a instância e configurar os mesmos parâmetros por ele; esse painel não está incluído no Compose.

Configuração da instância:

- Evento: `MESSAGES_UPSERT`.
- URL no Compose: `http://callmed:10000/api/atendimento/whatsapp/evolution`.
- Webhook por evento: desabilitado, para não acrescentar sufixos à rota.
- Cabeçalho: `X-CallMed-Webhook-Secret` com o mesmo valor de `CALLMED_WEBHOOK_SECRET`.
- O cabeçalho legado `X-MKSAN-Webhook-Secret` continua aceito. Prefira cabeçalho a chave em query string.

Em seguida entre no CallMed como administrador e vá a **Integrações → Verificar conexão com a Evolution**. Um estado conectado confirma apenas a sessão da instância; valide uma conversa real de teste para comprovar ida e volta.

## Se o CallMed já está hospedado

Não precisa mover o CRUD. Você pode rodar só os serviços da Evolution:

```powershell
docker compose --env-file .env.evolution -f compose.evolution.yml up -d evolution-api
```

O Compose ainda exige o arquivo preenchido para interpolação. Publique a Evolution por HTTPS através de proxy e configure no servidor do CallMed:

```dotenv
Atendimento__WhatsApp__Evolution__Enabled=true
Atendimento__WhatsApp__Evolution__BaseUrl=https://evolution.seu-dominio.com
Atendimento__WhatsApp__Evolution__ApiKey=SUA_CHAVE_EVOLUTION
Atendimento__WhatsApp__Evolution__InstanceName=callmed
Atendimento__WhatsApp__Evolution__WebhookSecret=SEU_SEGREDO_WEBHOOK
Atendimento__WhatsApp__Evolution__PublicNumber=55DDDNUMERO
```

Webhook: `https://seu-callmed.com/api/atendimento/whatsapp/evolution`, com o cabeçalho secreto. No script use `-BaseUrl` e `-WebhookUrl` para esses endereços. O backend precisa alcançar a Evolution, e a Evolution precisa alcançar o webhook do backend.

`localhost` dentro de um container aponta para o próprio container. No Compose fornecido, os nomes `evolution-api` e `callmed` resolvem pela rede interna. Se o CallMed roda no Visual Studio no Windows e a Evolution em Docker, o callback local pode apontar para `http://host.docker.internal:PORTA/api/atendimento/whatsapp/evolution`, usando a porta HTTP correta e acessível do projeto.

## Produção

Usar domínio/HTTPS e proxy configurado para o CallMed, `ASPNETCORE_ENVIRONMENT=Production`, credenciais em variáveis do servidor e volumes persistentes. Revise os cabeçalhos de proxy conforme a infraestrutura. O exemplo não provisiona domínio, certificados, backups nem monitoramento. Desative BootstrapAdmin depois do provisionamento. Reinicie mantendo os volumes; `docker compose down -v` apaga os dados persistidos.

O serviço precisa permanecer ligado para receber mensagens e executar lembretes. O número precisa estar conectado. Validar SMS, SMTP e integração com HIS continua sendo uma etapa separada.

## Referências oficiais consultadas

- Instalação Docker e dependências: https://docs.evolutionfoundation.com.br/en/evolution-api/install/docker
- Estado da instância: https://docs.evolutionfoundation.com.br/en/evolution-api/get-connection-state
- Configuração do webhook: https://docs.evolutionfoundation.com.br/evolution-api/set-webhook
- Releases: https://github.com/evolution-foundation/evolution-api/releases

Os payloads de configuração variam entre versões: use a documentação correspondente à imagem instalada e mantenha o teste de ida e volta como critério de aceitação.
