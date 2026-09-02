# CallMed V21.6

> **Todos os canais. Uma única agenda.**

Plataforma ASP.NET Core MVC para centralizar solicitações de consulta, triagem administrativa, agendas médicas, confirmações, lista de espera e atendimento omnichannel.

## O que existe no projeto

- solicitações e triagem omnichannel;
- pacientes, médicos, funcionários e especialidades;
- convênio × especialidade e elegibilidade;
- agenda, disponibilidades e exceções;
- agendamento, confirmação, remarcação, cancelamento e ausência;
- lembretes automáticos;
- lista de espera;
- Central de Atendimento com IA e atendimento humano;
- WhatsApp via Evolution API, e-mail e SMS opcional;
- auditoria e indicadores ligados ao desafio;
- acessibilidade e Modo Fácil;
- PWA e Android/TWA;
- API protegida para sistemas legados.

## Estrutura da solução

```text
CallMed/
├── CallMedCrud/                 # aplicação ASP.NET Core MVC
│   ├── Controllers/             # controllers separados por área
│   ├── Data/                    # DbContext, Identity e configurações EF
│   ├── DTOs/                    # contratos HTTP/IA/integrações
│   ├── Extensions/              # DI, pipeline e endpoints
│   ├── Middleware/
│   ├── Models/                  # entidades, enums e constantes
│   ├── Options/                 # configurações tipadas
│   ├── Services/                # regras, canais e integrações
│   ├── ViewModels/              # modelos exclusivos das Views
│   ├── Views/
│   └── wwwroot/
├── CallMedTWA/                  # Android Trusted Web Activity
├── PlayStore/                   # materiais da publicação
├── docs/                        # documentação
├── .env.example
├── CallMed.sln
└── Dockerfile
```

A estrutura detalhada está em `docs/ARQUITETURA.md`.

## Organização adotada na V21.6

- Controllers agora contêm **somente controllers**.
- Entidades persistidas ficam em `Models/Entities`.
- ViewModels não ficam mais misturados com Models.
- DTOs do agente, canais e API legada ficam em `DTOs`.
- Options de Gemini/SMTP/Evolution/SMS ficam em `Options`.
- Mapeamentos do Entity Framework ficam em `Data/Configurations`.
- Interfaces de serviços ficam em `Contracts`.
- Workers ficam em `Background`.
- Patches de banco ficam em `Services/Database/Initializers`.
- `Program.cs` ficou pequeno; bootstrap, DI e pipeline foram separados por responsabilidade.

Os namespaces das entidades EF e do `Usuario` foram preservados por compatibilidade com o banco/migrations existentes.

## Executar localmente

Requer .NET 8 SDK e PostgreSQL.

```bash
dotnet restore
dotnet run --project CallMedCrud/CallMedCrud.csproj
```

Ou abra `CallMed.sln` no Visual Studio.

## Configuração

Use `.env.example` como referência. Credenciais reais devem ficar apenas nas variáveis de ambiente.

Principais valores:

```env
ConnectionStrings__MKSANContextConnection=

Gemini__ApiKey=
Gemini__Model=gemini-3.1-flash-lite

LegacyIntegration__ApiKey=

Atendimento__WhatsApp__Evolution__Enabled=false
Atendimento__WhatsApp__Evolution__BaseUrl=
Atendimento__WhatsApp__Evolution__ApiKey=
Atendimento__WhatsApp__Evolution__InstanceName=mksan
Atendimento__WhatsApp__Evolution__PublicNumber=
Atendimento__WhatsApp__Evolution__WebhookSecret=
```

## Banco Neon

Os patches de schema são idempotentes. Para um banco Neon existente, mantenha `Database__AutoMigrate=false` enquanto o histórico de migrations não for deliberadamente revisado.

## PWA

- `wwwroot/manifest.json`
- `wwwroot/service-worker.js`
- cache da release: `v21-6-organized`

Páginas clínicas/autenticadas não são usadas como banco offline. Operações que dependem de vaga em tempo real continuam exigindo conexão.

A correção do limite de `FileSystemWatcher` no Render continua preservada: Views usam query string fixa de versão em vez de `asp-append-version`.

## Android / TWA

- versão: **21.6.0**;
- `CallMedTWA/README-TWA.md`;
- publicação: `docs/deploy/PLAY-STORE-PUBLICACAO.md`.

## Documentação

- `docs/ARQUITETURA.md` — organização técnica;
- `docs/DESAFIO-SENAI.md` — problema e solução;
- `docs/integracoes/INTEGRACAO-LEGADO.md` — API legada;
- `docs/deploy/PLAY-STORE-PUBLICACAO.md` — publicação Android;
- `docs/deploy/V21.5.1-RENDER-HTTP500.md` — correção do Render preservada;
- `docs/CHANGELOG-V21.6.md` — reorganização desta release;
- `docs/validacao/VALIDACAO.md` — validação atual.

## Deploy no Render

O `Dockerfile` publica `CallMedCrud/CallMedCrud.csproj` em .NET 8. Neon, Gemini, Evolution API, SMTP e demais segredos devem ser configurados somente no ambiente do serviço.
