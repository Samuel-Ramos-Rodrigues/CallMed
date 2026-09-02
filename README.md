<p align="center">
  <img src="CallMedCrud/wwwroot/images/logo-callmed-horizontal.png" alt="CallMed" width="300" />
</p>

<h1 align="center">CallMed</h1>

<p align="center">
  <strong>Todos os canais. Uma única agenda.</strong><br/>
  Plataforma omnichannel para tornar a marcação de consultas mais rápida, acessível, padronizada e integrada.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="ASP.NET Core MVC" src="https://img.shields.io/badge/ASP.NET%20Core-MVC-5C2D91?logo=dotnet&logoColor=white" />
  <img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-Neon-336791?logo=postgresql&logoColor=white" />
  <img alt="PWA" src="https://img.shields.io/badge/PWA-Ready-0A9B62?logo=pwa&logoColor=white" />
  <img alt="Android TWA" src="https://img.shields.io/badge/Android-TWA-3DDC84?logo=android&logoColor=white" />
  <img alt="Docker" src="https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white" />
  <img alt="Render" src="https://img.shields.io/badge/Deploy-Render-46E3B7" />
  <img alt="Versão" src="https://img.shields.io/badge/versão-21.6.0-0B5D4C" />
</p>

<p align="center">
  <a href="https://callmed.onrender.com">Aplicação</a> ·
  <a href="docs/ARQUITETURA.md">Arquitetura</a> ·
  <a href="docs/DESAFIO-SENAI.md">Desafio SENAI</a> ·
  <a href="docs/integracoes/INTEGRACAO-LEGADO.md">Integração legada</a> ·
  <a href="docs/validacao/VALIDACAO.md">Validação</a>
</p>

Sumário

Sobre o projeto

Problema que a CallMed resolve

Objetivos

Visão da solução

Principais funcionalidades

Perfis e permissões

Fluxo de marcação

Arquitetura

Tecnologias

Estrutura do projeto

Banco de dados

Inteligência artificial

Atendimento omnichannel

Lista de espera e redução de absenteísmo

PWA e acessibilidade

Android / TWA

Integração com sistemas legados

Segurança

Variáveis de ambiente

Executando localmente

Banco Neon e migrations

Deploy no Render

Health check

Validação atual

Solução de problemas

Documentação complementar

Contexto acadêmico

Sobre o projeto

A CallMed é uma plataforma web de gestão e marcação de consultas criada para centralizar um processo que normalmente acontece de forma fragmentada entre recepção, telefone, WhatsApp, e-mail e sistemas internos.

O sistema reúne em uma única aplicação:

solicitação de atendimento;

triagem administrativa;

cadastro de pacientes;

convênios e elegibilidade;

médicos e especialidades;

agenda e disponibilidade;

agendamento e remarcação;

confirmações e lembretes;

lista de espera;

atendimento humano e por IA;

histórico e auditoria;

indicadores operacionais;

PWA acessível para pacientes;

integração com canais externos e sistemas legados.

A proposta não é substituir o atendimento humano. A CallMed busca organizar o fluxo, automatizar tarefas repetitivas e deixar a equipe disponível para situações que realmente exigem intervenção humana.

Posicionamento: Todos os canais. Uma única agenda.

Problema que a CallMed resolve

O projeto nasceu de um desafio sobre a ineficiência no processo de marcação de consultas.

Em muitos ambientes de saúde, a solicitação pode chegar presencialmente, por telefone, WhatsApp ou e-mail. A equipe precisa conferir cadastro, convênio, especialidade, regras e agenda, muitas vezes usando sistemas diferentes ou até planilhas paralelas.

Isso pode gerar:

tempo elevado entre solicitação e confirmação;

informações diferentes entre os canais;

retrabalho da recepção;

dificuldade para localizar horários realmente disponíveis;

baixa visibilidade do histórico do paciente;

dificuldade de acesso para idosos e pessoas com baixa familiaridade tecnológica;

esquecimentos e absenteísmo;

vagas desperdiçadas após cancelamentos;

dependência excessiva de processos manuais.

A CallMed transforma esse cenário em um fluxo único, rastreável e padronizado.

Objetivos

Objetivo

Como a CallMed responde

Agilidade operacional

Centralização de solicitações, triagem e disponibilidade em um único fluxo.

Melhor experiência do paciente

PWA simples, consultas, confirmação e acompanhamento em poucos passos.

Acessibilidade e inclusão

Modo Fácil, texto ampliado, alto contraste e manutenção dos canais telefone/presencial.

Redução de absenteísmo

Confirmações, lembretes automáticos e tratamento de ausência.

Visão integrada

Pacientes, consultas, solicitações, conversas e agenda na mesma base.

Padronização

Status, triagem, regras de convênio, auditoria e fluxo operacional definidos.

Otimização da equipe

IA no primeiro contato, automações e central de atendimento.

Aproveitamento da agenda

Lista de espera capaz de reaproveitar vagas canceladas.

Integração

API para sistemas legados, webhooks e canais configuráveis.

Baixo custo

Stack web baseada em .NET, PostgreSQL/Neon e integrações opcionais.

Sustentabilidade

Registros digitais e redução do uso de papel/processos paralelos.

Visão da solução

flowchart LR
    A[Paciente] --> B{Canal}
    B -->|PWA / Site| C[Solicitação]
    B -->|WhatsApp| C
    B -->|Telefone| C
    B -->|Presencial| C
    B -->|E-mail| C
    B -->|SMS| C

    C --> D[Triagem administrativa]
    D --> E[Cadastro + Convênio + Especialidade]
    E --> F[Agenda e disponibilidade]
    F --> G[Agendamento]
    G --> H[Confirmação e lembretes]

    H -->|Confirmar| I[Consulta]
    H -->|Remarcar| F
    H -->|Cancelar| J[Lista de espera]
    J --> F

    I --> K[Histórico + Auditoria + Indicadores]

A mesma solicitação acompanha o paciente desde o primeiro contato até a confirmação, evitando que a informação se perca ao trocar de canal ou atendente.

Principais funcionalidades

Solicitações e triagem

entrada padronizada de solicitações;

identificação do canal de origem;

associação com paciente, especialidade e médico;

triagem administrativa;

validação de convênio e elegibilidade;

preferências de data/período;

pendências e observações;

responsável pela triagem;

histórico dos estados da solicitação;

liberação excepcional com justificativa e auditoria.

Agenda médica

cadastro de médicos e especialidades;

agenda semanal por médico;

múltiplas sessões por dia;

disponibilidades;

exceções e bloqueios;

prevenção de conflito de horário;

consulta de vagas reais;

busca por médico ou especialidade;

agenda geral para equipe interna.

Consultas

criação de consulta;

consulta pelo paciente ou pela equipe;

confirmação;

remarcação;

cancelamento;

registro de ausência;

manutenção do mesmo médico na remarcação quando exigido pelo fluxo;

histórico do paciente.

Convênios

cadastro dos dados de convênio do paciente;

validade;

matriz Convênio × Especialidade;

checagem de elegibilidade durante a triagem;

atendimento particular quando aplicável.

Central de Atendimento

caixa de entrada centralizada;

conversas por canal;

IA no primeiro atendimento;

atendimento humano;

assumir conversa;

devolver atendimento para IA;

encerrar e reabrir conversa;

acesso ao contexto do paciente durante o atendimento;

envio por canal configurado.

Confirmações e lembretes

lembretes automáticos de consulta;

confirmação de presença;

suporte a cancelamento/remarcação;

hosted service para processamento em segundo plano;

canal preferido do paciente.

Lista de espera

entrada voluntária na fila;

preferência por médico/especialidade;

data e período preferidos;

detecção de vagas;

oferta de vaga;

aceite pelo paciente;

revalidação da disponibilidade antes da reserva.

Pacientes

cadastro administrativo;

CPF, contato e dados pessoais;

convênio;

canal preferido;

histórico de consultas;

solicitações e atendimentos relacionados;

status ativo/inativo.

Auditoria e relatórios

registro de eventos relevantes;

identificação de alterações;

rastreabilidade administrativa;

indicadores operacionais;

métricas relacionadas a confirmação e absenteísmo;

distribuição por canal;

acompanhamento de solicitações pendentes.

Perfis e permissões

A autenticação é feita com ASP.NET Core Identity e utiliza quatro roles principais.

Perfil

Principais acessos

Paciente

Home/PWA, próprias consultas, agendamento, confirmações, lista de espera, conta e assistente.

Médico

Painel e agenda relacionados ao atendimento médico.

Funcionário

Pacientes, médicos, agenda, consultas, solicitações, triagem, Central de Atendimento e relatórios.

Admin

Todos os recursos administrativos, funcionários, especialidades, convênios, integrações, auditoria e configurações.

A aplicação não depende apenas de botões escondidos na interface: os Controllers possuem regras de autorização por role no servidor.

Fluxo de marcação

1. Solicitação

O paciente entra em contato por um dos canais disponíveis.

2. Triagem administrativa

A equipe verifica:

identificação do paciente;

situação cadastral;

convênio;

validade;

especialidade solicitada;

elegibilidade;

preferências e observações.

3. Disponibilidade

O backend cruza:

Especialidade
    +
Médico
    +
Agenda semanal
    +
Disponibilidades
    +
Exceções / bloqueios
    +
Consultas existentes
    =
Vagas realmente disponíveis

4. Agendamento

A vaga escolhida é gravada no sistema e vinculada ao fluxo do paciente.

5. Confirmação

O paciente pode confirmar, remarcar ou cancelar.

6. Lista de espera

Uma vaga cancelada pode voltar ao fluxo e ser oferecida a um paciente compatível da lista de espera.

7. Histórico e indicadores

O resultado alimenta o histórico clínico-administrativo, auditoria e os indicadores operacionais.

Arquitetura

A V21.6 foi reorganizada para separar responsabilidades sem introduzir complexidade desnecessária para o tamanho atual da aplicação.

flowchart TD
    UI[Views / PWA] --> CTL[Controllers]
    CTL --> VM[ViewModels / DTOs]
    CTL --> SVC[Services]
    SVC --> DB[MKSANContext / EF Core]
    DB --> PG[(PostgreSQL / Neon)]

    SVC --> GEMINI[Gemini]
    SVC --> EVO[Evolution API]
    SVC --> SMTP[SMTP]
    SVC --> SMS[Gateway SMS]
    API[API / Webhooks] --> SVC
    LEGADO[HIS / Sistema legado] --> API

Responsabilidades

Controller
    ↓
Service
    ↓
Data / DbContext
    ↓
PostgreSQL

Para a camada de interface:

Controller
    ↓
ViewModel
    ↓
View

Regras importantes da organização:

Controllers contêm apenas Controllers;

Entities representam dados persistidos;

DTOs representam contratos externos;

ViewModels representam dados específicos de telas;

Services concentram regras reutilizáveis;

Options representam configurações tipadas;

Data/Configurations contém os mapeamentos do EF Core;

workers ficam em Background;

interfaces ficam em Contracts.

Mais detalhes: docs/ARQUITETURA.md.

Tecnologias

Camada

Tecnologia

Backend

.NET 8 / ASP.NET Core MVC

ORM

Entity Framework Core 8

Banco

PostgreSQL

Banco em nuvem

Neon

Autenticação

ASP.NET Core Identity + Roles

Front-end

Razor Views, HTML, CSS e JavaScript

PWA

Web App Manifest + Service Worker

Android

Trusted Web Activity (TWA)

IA

Google Gemini via API HTTP

WhatsApp

Evolution API

E-mail

SMTP + webhook inbound opcional

SMS

Gateway HTTP configurável

Container

Docker

Deploy

Render

CI Android

GitHub Actions

Pacotes principais do projeto:

Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.20
Microsoft.AspNetCore.Identity.UI 8.0.20
Microsoft.EntityFrameworkCore.Design 8.0.20
Microsoft.EntityFrameworkCore.Tools 8.0.20
Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11

Estrutura do projeto

CallMed/
│
├── CallMedCrud/                       # Aplicação ASP.NET Core MVC
│   ├── Areas/
│   │   └── Identity/                  # Login, cadastro e recuperação de conta
│   │
│   ├── Controllers/
│   │   ├── Admin/                     # Auditoria, configurações e integrações
│   │   ├── Agenda/                    # Consulta, agenda e disponibilidade
│   │   ├── Api/                       # Webhooks e integração legada
│   │   ├── Atendimento/               # Central, IA, confirmações e solicitações
│   │   ├── Cadastros/                 # Paciente, médico, funcionário, convênio
│   │   ├── Conta/                     # Minha Conta
│   │   └── Paineis/                   # Home, dashboards e relatórios
│   │
│   ├── Data/
│   │   ├── Configurations/            # IEntityTypeConfiguration do EF Core
│   │   ├── Identity/                  # Usuario do Identity
│   │   └── MKSANContext.cs
│   │
│   ├── DTOs/
│   │   ├── Agente/
│   │   ├── Atendimento/
│   │   └── Integracao/
│   │
│   ├── Extensions/                    # DI, middleware e endpoints
│   ├── Middleware/
│   ├── Migrations/
│   │
│   ├── Models/
│   │   ├── Constants/
│   │   ├── Entities/
│   │   │   ├── Agente/
│   │   │   ├── Atendimento/
│   │   │   └── Clinica/
│   │   └── Enums/
│   │
│   ├── Options/                       # Configurações fortemente tipadas
│   │
│   ├── Services/
│   │   ├── Agendamento/
│   │   ├── Agente/
│   │   ├── Atendimento/
│   │   ├── Clinica/
│   │   │   └── Background/            # Lembretes, agenda e lista de espera
│   │   ├── Database/Initializers/     # Patches idempotentes
│   │   ├── Email/
│   │   ├── Startup/
│   │   └── Usuarios/
│   │
│   ├── ViewModels/
│   ├── Views/
│   ├── ViewComponents/
│   │
│   └── wwwroot/
│       ├── css/
│       ├── icons/
│       ├── images/
│       ├── js/
│       ├── manifest.json
│       ├── offline.html
│       └── service-worker.js
│
├── CallMedTWA/                        # Wrapper Android da PWA
├── PlayStore/                         # Materiais para publicação
├── docs/                              # Documentação técnica
├── .github/workflows/                 # Builds Android
├── .env.example                       # Referência das variáveis
├── Dockerfile
├── CallMed.sln
└── README.md

Banco de dados

A aplicação usa MKSANContext, baseado em IdentityDbContext<Usuario>, com PostgreSQL através do provider Npgsql.

Principais entidades

O contexto possui 16 conjuntos principais:

Paciente
Funcionario
Medico
Especialidade
Disponibilidade
MedicoHorarioSemanal
Consulta
ConversaAgente
MensagemConversaAgente
ConversaAtendimento
MensagemAtendimento
ListaEspera
AgendaExcecao
SolicitacaoAtendimento
ConvenioEspecialidade
AuditoriaEvento

Cada entidade persistida possui configuração do Entity Framework em Data/Configurations.

Os namespaces originais de entidades e Identity foram preservados na reorganização da V21.6 para não criar alterações artificiais no snapshot das migrations nem no banco Neon existente.

Inteligência artificial

A CallMed possui um assistente baseado em Gemini, utilizado como recepcionista virtual e apoio ao fluxo de atendimento.

O agente não utiliza memória textual como fonte de verdade para disponibilidade. Quando precisa de dados atuais, chama ferramentas internas da aplicação.

Entre as ações disponíveis para o agente estão:

listar médicos reais;

consultar as consultas do paciente;

localizar paciente por CPF quando o usuário autorizado é funcionário/admin;

buscar horários em uma data;

buscar próximas vagas;

encontrar opções de agendamento;

preparar e efetivar agendamento após confirmação;

confirmar consulta;

preparar e efetivar remarcação;

cancelar consulta após confirmação;

entrar, consultar ou sair da lista de espera;

cadastrar paciente em fluxo administrativo autorizado;

consultar informações oficiais da clínica.

Princípio do agente

Proativo para consultar. Conservador para alterar.

A IA não deve:

inventar médicos ou horários;

diagnosticar doenças;

afirmar disponibilidade sem consultar o backend;

alterar dados sem confirmação explícita;

pedir CPF do próprio paciente autenticado quando o sistema já conhece sua identidade.

Configuração:

Gemini__ApiKey=
Gemini__Model=gemini-3.1-flash-lite
Gemini__TimeoutSeconds=60
Gemini__MaxToolRounds=8
Gemini__MaxHistoryMessages=30

Atendimento omnichannel

A Central de Atendimento foi projetada para reunir diferentes canais no mesmo histórico.

Canais previstos pelo backend:

Web/PWA;

WhatsApp;

SMS;

e-mail;

telefone/presencial através da operação interna de solicitações.

WhatsApp — Evolution API

Atendimento__WhatsApp__Evolution__Enabled=true
Atendimento__WhatsApp__Evolution__BaseUrl=
Atendimento__WhatsApp__Evolution__ApiKey=
Atendimento__WhatsApp__Evolution__InstanceName=mksan
Atendimento__WhatsApp__Evolution__PublicNumber=
Atendimento__WhatsApp__Evolution__WebhookSecret=

Webhook:

POST /api/atendimento/whatsapp/evolution?secret=SEU_SEGREDO

Também é aceito o header:

X-MKSAN-Webhook-Secret: SEU_SEGREDO

SMS

O SMS utiliza um sender HTTP genérico para permitir troca de provedor sem acoplar o domínio a um fornecedor específico.

Atendimento__Sms__Http__Enabled=false
Atendimento__Sms__Http__BaseUrl=
Atendimento__Sms__Http__SendPath=/messages
Atendimento__Sms__Http__ApiKey=
Atendimento__Sms__Http__ApiKeyHeader=Authorization
Atendimento__Sms__Http__ApiKeyScheme=Bearer
Atendimento__Sms__Http__Sender=CallMed
Atendimento__Sms__Http__WebhookSecret=

Webhook inbound:

POST /api/atendimento/sms?secret=SEU_SEGREDO

E-mail

O envio utiliza SMTP. O recebimento pode ser integrado por webhook de um provedor inbound.

Atendimento__Email__Inbound__Enabled=false
Atendimento__Email__Inbound__WebhookSecret=

Webhook:

POST /api/atendimento/email?secret=SEU_SEGREDO

Status dos canais

Funcionários e administradores podem consultar:

GET /api/atendimento/status

Lista de espera e redução de absenteísmo

A redução de vagas ociosas acontece através da combinação de três recursos.

1. Lembretes

LembreteConsultaBackgroundService procura consultas próximas e processa os lembretes conforme o fluxo configurado.

2. Confirmação

O paciente pode confirmar presença; o sistema também registra cancelamento, remarcação e ausência para gerar indicadores reais.

3. Lista de espera

ListaEsperaBackgroundService processa os pedidos ativos e ajuda a reaproveitar vagas disponíveis.

Antes de reservar, a disponibilidade é validada novamente para evitar duas pessoas utilizando a mesma vaga.

PWA e acessibilidade

A CallMed funciona como Progressive Web App.

Arquivos principais:

CallMedCrud/wwwroot/manifest.json
CallMedCrud/wwwroot/service-worker.js
CallMedCrud/wwwroot/offline.html

Cache atual da release:

callmed-static-v21-6-organized

Recursos PWA

instalação pelo navegador compatível;

execução standalone;

ícones normal e maskable;

atalhos para consultas, agendamento e lista de espera;

interface responsiva;

tratamento de safe-area;

tela offline;

cache apenas de assets adequados.

Páginas clínicas autenticadas não são tratadas como banco offline. Marcar ou alterar uma consulta exige conexão porque a disponibilidade precisa ser validada em tempo real.

Acessibilidade

A interface inclui recursos voltados especialmente a idosos e pessoas com pouca familiaridade digital:

Modo Fácil;

aumento de texto;

alto contraste;

redução de animações;

suporte a leitura do conteúdo;

botões com áreas de toque maiores;

fluxo simplificado no celular;

linguagem direta;

possibilidade de continuar o atendimento por telefone ou presencialmente.

Android / TWA

A PWA possui um wrapper Android baseado em Trusted Web Activity.

Configuração atual:

applicationId: com.callmed.app
minSdk: 26
targetSdk: 36
versionCode: 2160
versionName: 21.6.0

Arquivos úteis:

CallMedTWA/GERAR-APK-DEBUG.bat
CallMedTWA/GERAR-AAB-PLAY.bat
CallMedTWA/GERAR-AAB-PLAY.sh
CallMedTWA/README-TWA.md

A associação entre site e aplicativo é exposta dinamicamente em:

/.well-known/assetlinks.json

Configure as impressões SHA-256 da assinatura:

Twa__Enabled=true
Twa__PackageName=com.callmed.app
Twa__Origin=https://callmed.onrender.com
Twa__Sha256CertFingerprints__0=
Twa__Sha256CertFingerprints__1=

Nunca publique o keystore de produção. O debug.keystore existente serve somente para desenvolvimento/teste.

Integração com sistemas legados

A CallMed possui uma API mínima para interoperabilidade com HIS, agendas ou cadastros externos.

Ela só fica operacional quando uma chave é definida:

LegacyIntegration__ApiKey=UMA_CHAVE_LONGA_E_ALEATORIA

Toda chamada deve enviar:

X-CallMed-Integration-Key: SUA_CHAVE

Endpoints

GET  /api/integracao/v1/status
GET  /api/integracao/v1/pacientes/cpf/{cpf}
GET  /api/integracao/v1/disponibilidade?especialidade=...
POST /api/integracao/v1/solicitacoes

A API foi mantida propositalmente pequena: sistemas externos entram no mesmo fluxo da CallMed em vez de duplicarem a lógica de agenda.

Documentação completa: docs/integracoes/INTEGRACAO-LEGADO.md.

Segurança

Algumas proteções presentes no projeto:

ASP.NET Core Identity;

roles no servidor;

senha mínima de 8 caracteres;

bloqueio após 5 tentativas inválidas;

lockout de 15 minutos;

e-mail único por usuário;

cookie HttpOnly;

cookie Secure em produção;

SameSite=Lax;

expiração de sessão em 8 horas com sliding expiration;

token de proteção com duração de 1 hora;

HSTS fora do ambiente de desenvolvimento;

HTTPS redirect;

suporte a forwarded headers para reverse proxy;

rate limiting para IA;

rate limiting para webhooks/APIs;

comparação em tempo constante para segredos de webhook/API;

segredo separado para cada canal inbound;

auditoria administrativa;

páginas clínicas fora do cache offline;

credenciais mantidas em variáveis de ambiente.

Rate limiting

Agente:

20 requisições / minuto por usuário/IP
fila máxima: 2

Webhooks e integração:

120 requisições / minuto por IP
sem fila

Variáveis de ambiente

Use .env.example como referência dos nomes. A aplicação lê configuração pelo sistema padrão do ASP.NET Core; as credenciais devem ser configuradas como variáveis do ambiente ou através de uma configuração local que não seja enviada ao Git.

Banco

ConnectionStrings__MKSANContextConnection=
Database__AutoMigrate=false
Database__ApplyV12Patch=true
Database__ApplyV13Patch=true
Database__ApplyV14Patch=true
Database__ApplyV15Patch=true
Database__ApplyV16Patch=true
Database__ApplyV21Patch=true

Clínica

Clinica__Nome=CallMed
Clinica__Endereco=
Clinica__Telefone=
Clinica__Whatsapp=
Clinica__Email=
Clinica__HorarioFuncionamento=
Clinica__FormasPagamento=
Clinica__ConveniosAceitos=

Gemini

Gemini__ApiKey=
Gemini__Model=gemini-3.1-flash-lite
Gemini__TimeoutSeconds=60
Gemini__MaxToolRounds=8
Gemini__MaxHistoryMessages=30

SMTP

Smtp__Host=
Smtp__Port=587
Smtp__Username=
Smtp__Password=
Smtp__FromEmail=
Smtp__FromName=CallMed
Smtp__EnableSsl=true

Primeiro administrador

BootstrapAdmin__Enabled=false
BootstrapAdmin__Email=
BootstrapAdmin__Password=
BootstrapAdmin__Name=

Quando BootstrapAdmin__Enabled=true, o startup cria/verifica o usuário, atribui a role Admin e cria/atualiza seu vínculo como funcionário administrador.

Depois do primeiro deploy, desative essa opção.

Para todas as demais variáveis consulte .env.example.

Executando localmente

Pré-requisitos

.NET SDK 8

PostgreSQL 14+ ou banco Neon

Git

Visual Studio 2022, Rider ou VS Code (opcional)

1. Clone o repositório

git clone SEU_REPOSITORIO.git
cd CallMed

2. Restaure os pacotes

dotnet restore CallMed.sln

3. Configure a conexão

Exemplo de variável no PowerShell:

$env:ConnectionStrings__MKSANContextConnection="Host=SEU_HOST;Port=5432;Database=SEU_BANCO;Username=SEU_USUARIO;Password=SUA_SENHA;SSL Mode=Require"

No Linux/macOS:

export ConnectionStrings__MKSANContextConnection='Host=SEU_HOST;Port=5432;Database=SEU_BANCO;Username=SEU_USUARIO;Password=SUA_SENHA;SSL Mode=Require'

Não coloque uma senha real no repositório.

4. Opcional: crie o primeiro administrador

$env:BootstrapAdmin__Enabled="true"
$env:BootstrapAdmin__Email="admin@exemplo.com"
$env:BootstrapAdmin__Password="SenhaSegura123"
$env:BootstrapAdmin__Name="Administrador CallMed"

5. Execute

dotnet run --project CallMedCrud/CallMedCrud.csproj

Ou abra CallMed.sln no Visual Studio e execute o projeto CallMedCrud.

Banco Neon e migrations

Banco novo

Para um ambiente totalmente novo, o projeto pode executar as migrations quando:

Database__AutoMigrate=true

Alternativamente, com dotnet-ef instalado:

dotnet ef database update --project CallMedCrud/CallMedCrud.csproj

Banco Neon existente

Para um banco já utilizado pela CallMed, a configuração recomendada é:

Database__AutoMigrate=false

A aplicação possui initializers idempotentes para evolução de schema:

V12
V13
V14
V15
V16
V21

Eles ficam em:

CallMedCrud/Services/Database/Initializers/

Isso reduz o risco de aplicar migrations antigas novamente sobre um banco que já evoluiu durante o desenvolvimento.

Criar nova migration

Quando uma mudança futura realmente exigir uma migration EF:

dotnet ef migrations add NomeDaMigration \
  --project CallMedCrud/CallMedCrud.csproj

Sempre revise a migration gerada antes de aplicá-la em produção.

Deploy no Render

O repositório possui um Dockerfile preparado para publicação.

Dockerfile

O build:

usa o SDK .NET 8;

restaura o CallMedCrud.csproj;

executa dotnet publish -c Release;

copia o resultado para a imagem ASP.NET Runtime;

expõe a aplicação na porta 10000.

Criando o serviço

No Render:

crie um Web Service;

conecte o repositório;

selecione o deploy via Docker;

cadastre as variáveis de ambiente;

use /health como Health Check Path, se desejar;

faça o deploy.

Variáveis essenciais em produção

No mínimo:

ConnectionStrings__MKSANContextConnection
Gemini__ApiKey                    # se IA estiver ativa
BootstrapAdmin__...              # somente no primeiro bootstrap
LegacyIntegration__ApiKey        # somente se API legada estiver ativa
Atendimento__...                 # canais que forem utilizados

Proteção para Render/Linux

O projeto preserva a correção de um problema de FileSystemWatcher/inotify observado no Render:

DOTNET_USE_POLLING_FILE_WATCHER=1
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

Além disso, as Views não utilizam asp-append-version, evitando que o Razor crie watchers adicionais para versionamento de assets.

Mais detalhes: docs/deploy/V21.5.1-RENDER-HTTP500.md.

Health check

A aplicação possui um endpoint simples para monitoramento:

GET /health

Resposta esperada:

{
  "status": "ok",
  "service": "CallMed"
}

Validação atual

Na reorganização V21.6 foram executadas validações estáticas sobre o pacote.

Verificação

Resultado

JSON (appsettings.json / manifest)

✅

JavaScript / Service Worker (node --check)

✅

CSS (tinycss2)

✅

Assets do Service Worker

✅ 21/21

Controllers apenas com classes Controller

✅

DTOs separados

✅

ViewModels separados

✅

Entidades separadas

✅

MKSANContext único

✅

Usuario único

✅

16 DbSets / 16 configurações EF

✅

asp-append-version

✅ 0 ocorrências

Docker com correção de watcher

✅

TWA

✅ 21.6.0

Cache PWA

✅ callmed-static-v21-6-organized

Limitação da validação realizada

No ambiente em que a release foi organizada, o SDK .NET não estava disponível. Por isso o comando dotnet build não pôde ser executado naquela etapa; a revisão utilizada foi estrutural e sintática.

Em uma máquina com .NET SDK, antes de publicar uma alteração nova, execute:

dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release

Solução de problemas

HTTP 500 no Render com inotify

Sintoma típico:

System.IO.IOException:
The configured user limit on the number of inotify instances has been reached

A V21.6 já contém a correção:

asp-append-version removido;

versionamento de assets por query string fixa;

polling file watcher habilitado;

reload de configuração desabilitado no container.

Não reintroduza asp-append-version="true" em massa nas Views durante futuras alterações.

CSS/PWA continua mostrando uma versão antiga

faça o deploy da nova versão;

feche completamente o PWA;

abra novamente;

em caso de desenvolvimento, limpe o Service Worker/cache do site.

O service-worker.js é servido com no-cache, no-store, must-revalidate para facilitar atualização de releases.

API legada retorna 503

A integração permanece desligada enquanto isto estiver vazio:

LegacyIntegration__ApiKey=

Isso é intencional.

WhatsApp aparece como não configurado

Confira:

Atendimento__WhatsApp__Evolution__Enabled=true
Atendimento__WhatsApp__Evolution__BaseUrl=
Atendimento__WhatsApp__Evolution__ApiKey=
Atendimento__WhatsApp__Evolution__InstanceName=
Atendimento__WhatsApp__Evolution__WebhookSecret=

Depois consulte, autenticado como funcionário/admin:

/api/atendimento/status

Não consigo entrar como administrador no primeiro deploy

Ative temporariamente:

BootstrapAdmin__Enabled=true

Configure e-mail, senha e nome, faça o deploy e depois volte para:

BootstrapAdmin__Enabled=false

Documentação complementar

Documento

Conteúdo

docs/ARQUITETURA.md

Organização técnica da V21.6

docs/DESAFIO-SENAI.md

Relação entre o problema proposto e a solução

docs/integracoes/INTEGRACAO-LEGADO.md

API para HIS/sistemas existentes

docs/deploy/V21.5.1-RENDER-HTTP500.md

Correção do problema de inotify no Render

docs/deploy/PLAY-STORE-PUBLICACAO.md

Publicação Android

docs/CHANGELOG-V21.6.md

Reorganização da release

docs/validacao/VALIDACAO.md

Checklist de validação

CallMedTWA/README-TWA.md

Wrapper Android/TWA

Contexto acadêmico

A CallMed foi desenvolvida a partir de um desafio educacional sobre:

“Como tornar o processo de marcação de consultas mais eficiente e acessível.”

Instituição do desafio:

SENAI Vitória da Conquista — Bahia

A solução foi direcionada aos requisitos do problema original, priorizando:

acessibilidade;

baixo custo;

atendimento híbrido;

integração com estruturas existentes;

redução de tarefas manuais;

padronização;

diminuição de absenteísmo;

melhor aproveitamento da agenda médica.

Funcionalidades que não contribuem diretamente para esse problema, como marketplace, pontos, clube de descontos ou recursos de “super app”, não fazem parte do escopo principal atual.

Observação sobre saúde

A CallMed utiliza IA como assistente administrativo de atendimento e agendamento. O assistente não substitui avaliação médica e não deve ser utilizado para diagnóstico.

<p align="center">
  <strong>CallMed</strong><br/>
  Todos os canais. Uma única agenda.<br/>
  <sub>Agendamento mais rápido, acessível e conectado.</sub>
</p>
