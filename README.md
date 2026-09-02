<p align="center">
  <img src="./CallMedCrud/wwwroot/images/logo-callmed-horizontal.png" alt="CallMed" width="340">
</p>

<h1 align="center">CallMed</h1>

<p align="center">
  <strong>Todos os canais. Uma única agenda.</strong>
</p>

<p align="center">
  Plataforma omnichannel para centralizar solicitações, triagem, agendamento, confirmações e atendimento de uma clínica em um único fluxo.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/ASP.NET_Core-MVC-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt="ASP.NET Core MVC">
  <img src="https://img.shields.io/badge/PostgreSQL-Neon-4169E1?style=flat-square&logo=postgresql&logoColor=white" alt="PostgreSQL / Neon">
  <img src="https://img.shields.io/badge/PWA-Ready-5A0FC8?style=flat-square&logo=pwa&logoColor=white" alt="PWA">
  <img src="https://img.shields.io/badge/Android-TWA-3DDC84?style=flat-square&logo=android&logoColor=white" alt="Android TWA">
  <img src="https://img.shields.io/badge/Deploy-Render-46E3B7?style=flat-square" alt="Render">
</p>

<p align="center">
  <a href="https://callmed.onrender.com/"><strong>🌐 Acessar aplicação</strong></a>
  &nbsp;•&nbsp;
  <a href="./docs/ARQUITETURA.md">Arquitetura</a>
  &nbsp;•&nbsp;
  <a href="./docs/DESAFIO-SENAI.md">Desafio SENAI</a>
  &nbsp;•&nbsp;
  <a href="./docs/validacao/VALIDACAO.md">Validação</a>
</p>

Sobre a CallMed

A CallMed é uma plataforma web de gestão e marcação de consultas criada para reduzir a fragmentação entre recepção, telefone, WhatsApp, e-mail e canais digitais.

Em vez de cada canal funcionar como um processo separado, a CallMed transforma o atendimento em um fluxo único:

solicitação → triagem → disponibilidade → agendamento → confirmação → acompanhamento

A proposta é apoiar a equipe da clínica, reduzir tarefas repetitivas e oferecer ao paciente uma experiência simples e acessível — sem eliminar o atendimento humano, telefônico ou presencial.

Objetivo: tornar o processo de marcação de consultas mais rápido, acessível, padronizado e conectado.

O problema que o projeto resolve

Cenário comum

Como a CallMed responde

Solicitações espalhadas em vários canais

Centraliza tudo em um fluxo de atendimento

Demora para encontrar horários

Consulta disponibilidade real da agenda

Informações diferentes entre atendentes

Padroniza triagem, status e regras

Esquecimento de consultas

Trabalha com confirmações e lembretes

Vagas perdidas após cancelamentos

Utiliza lista de espera

Dificuldade de uso por alguns pacientes

PWA responsiva e recursos de acessibilidade

Retrabalho da recepção

Automatiza tarefas operacionais e oferece apoio por IA

Sistemas existentes isolados

Disponibiliza API de integração legada

Principais recursos







📅 Agenda inteligente
Agenda semanal, sessões, disponibilidade, exceções e prevenção de conflitos.

💬 Atendimento omnichannel
Web/PWA, WhatsApp, e-mail, SMS e registro de telefone/presencial.

🤖 Assistente com IA
Gemini conectado às informações reais do sistema para apoiar o atendimento.

🩺 Gestão de consultas
Agendamento, confirmação, remarcação, cancelamento e histórico.

🔁 Lista de espera
Ajuda a reaproveitar vagas liberadas e reduz horários ociosos.

✅ Triagem administrativa
Paciente, convênio, especialidade, preferências e elegibilidade.

♿ Acessibilidade
Modo Fácil, texto ampliado, alto contraste e interface mobile-first.

📊 Indicadores e auditoria
Acompanhamento operacional, rastreabilidade e eventos administrativos.

🔌 Integrações
Evolution API, SMTP, SMS HTTP, webhooks e API para sistemas legados.

Fluxo de atendimento

flowchart LR
    A[Paciente] --> B{Canal}

    B -->|PWA / Site| C[Solicitação]
    B -->|WhatsApp| C
    B -->|Telefone| C
    B -->|Presencial| C
    B -->|E-mail| C

    C --> D[Triagem]
    D --> E[Agenda]
    E --> F[Agendamento]
    F --> G[Confirmação]

    G -->|Confirmado| H[Consulta]
    G -->|Remarcar| E
    G -->|Cancelar| I[Lista de espera]
    I --> E

    H --> J[Histórico e indicadores]

Perfis do sistema

Perfil

Principais recursos

Paciente

Agendar, consultar, confirmar, remarcar/cancelar quando permitido, lista de espera, conta e assistente

Médico

Painel e agenda relacionados ao atendimento médico

Funcionário

Pacientes, médicos, agenda, triagem, consultas, Central de Atendimento e relatórios

Administrador

Recursos administrativos, funcionários, convênios, especialidades, integrações, auditoria e configurações

A autorização é validada no servidor com ASP.NET Core Identity + Roles.

Arquitetura

A V21.6 foi reorganizada para separar responsabilidades e manter o projeto simples de entender e evoluir.

flowchart LR
    UI[Views / PWA] --> C[Controllers]
    C --> V[ViewModels / DTOs]
    C --> S[Services]
    S --> D[EF Core / MKSANContext]
    D --> DB[(PostgreSQL / Neon)]

    S --> AI[Gemini]
    S --> WA[Evolution API]
    S --> MAIL[SMTP]
    S --> SMS[SMS HTTP]

Organização das camadas

CallMed/
├── CallMedCrud/
│   ├── Areas/Identity/          # Login, cadastro e recuperação
│   ├── Controllers/             # Entrada HTTP/MVC por domínio
│   ├── Data/                    # DbContext, Identity e configurações EF
│   ├── DTOs/                    # Contratos de APIs e integrações
│   ├── Extensions/              # DI, pipeline e endpoints
│   ├── Middleware/              # Middlewares próprios
│   ├── Migrations/              # Migrations do Entity Framework
│   ├── Models/                  # Entidades, enums e constantes
│   ├── Options/                 # Configurações tipadas
│   ├── Services/                # Regras de negócio e integrações
│   ├── ViewModels/              # Modelos específicos das telas
│   ├── Views/                   # Razor Views
│   └── wwwroot/                 # CSS, JS, PWA, imagens e ícones
│
├── CallMedTWA/                  # Aplicativo Android via TWA
├── PlayStore/                   # Materiais de publicação
├── docs/                        # Documentação técnica
├── .github/workflows/           # Automação de build
├── Dockerfile
└── CallMed.sln

Para detalhes, consulte docs/ARQUITETURA.md.

Tecnologias

Área

Tecnologia

Backend

.NET 8 / ASP.NET Core MVC

ORM

Entity Framework Core 8

Banco

PostgreSQL / Neon

Autenticação

ASP.NET Core Identity + Roles

Front-end

Razor, HTML, CSS e JavaScript

PWA

Web App Manifest + Service Worker

Android

Trusted Web Activity (TWA)

IA

Google Gemini

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

Banco de dados

O projeto utiliza MKSANContext, baseado em IdentityDbContext, com provider Npgsql para PostgreSQL.

Entre as principais entidades estão:

Paciente · Medico · Funcionario · Especialidade · Consulta · Disponibilidade · MedicoHorarioSemanal · ListaEspera · AgendaExcecao · SolicitacaoAtendimento · ConvenioEspecialidade · AuditoriaEvento · ConversaAtendimento · MensagemAtendimento

Os mapeamentos do Entity Framework ficam separados em:

CallMedCrud/Data/Configurations/

Assistente com IA

A CallMed utiliza Google Gemini como apoio ao atendimento administrativo.

O agente pode consultar ferramentas internas para trabalhar com dados reais do sistema, por exemplo:

médicos cadastrados;

próximas vagas;

horários disponíveis;

consultas do paciente;

confirmação e cancelamento;

remarcação;

lista de espera;

informações oficiais da clínica.

Princípio: a IA pode ajudar a consultar e conduzir o fluxo, mas não deve inventar médicos, horários, elegibilidade ou disponibilidade.

A IA é um recurso administrativo e não substitui avaliação médica nem realiza diagnóstico.

Central de Atendimento

A Central reúne conversas e contexto do paciente em um único local.

Canais previstos:

🌐 Web / PWA

💬 WhatsApp

✉️ E-mail

📱 SMS

☎️ Telefone

🏥 Presencial

No WhatsApp, a integração é feita através da Evolution API. E-mail e SMS permanecem configuráveis para permitir troca de provedor sem acoplar o sistema a um único fornecedor.

PWA e acessibilidade

A aplicação pode ser instalada como Progressive Web App em navegadores compatíveis.

Principais recursos:

instalação em modo standalone;

interface responsiva;

navegação mobile;

ícones normal e maskable;

tela offline;

safe-area para dispositivos móveis;

Modo Fácil;

texto ampliado;

alto contraste;

redução de animações;

suporte a leitura de conteúdo.

Operações de agenda não são tratadas como dados offline. Criar ou alterar uma consulta exige conexão para validar a disponibilidade em tempo real.

Android / TWA

O repositório também inclui um wrapper Android baseado em Trusted Web Activity.

applicationId: com.callmed.app
minSdk: 26
targetSdk: 36
versionName: 21.6.0

Arquivos e instruções estão em CallMedTWA/README-TWA.md.

Como executar localmente

Pré-requisitos

.NET SDK 8

PostgreSQL 14+ ou Neon

Git

1. Clone o projeto

git clone https://github.com/Samuel-Ramos-Rodrigues/CallMed.git
cd CallMed

2. Restaure as dependências

dotnet restore CallMed.sln

3. Configure o banco

PowerShell:

$env:ConnectionStrings__MKSANContextConnection="Host=HOST;Port=5432;Database=BANCO;Username=USUARIO;Password=SENHA;SSL Mode=Require"

Linux/macOS:

export ConnectionStrings__MKSANContextConnection='Host=HOST;Port=5432;Database=BANCO;Username=USUARIO;Password=SENHA;SSL Mode=Require'

4. Execute

dotnet run --project CallMedCrud/CallMedCrud.csproj

Não publique senhas, tokens ou connection strings reais no repositório.

Configuração

O arquivo .env.example lista as configurações disponíveis.

As principais são:

# Banco
ConnectionStrings__MKSANContextConnection=

# Gemini
Gemini__ApiKey=
Gemini__Model=gemini-3.1-flash-lite

# WhatsApp / Evolution API
Atendimento__WhatsApp__Evolution__Enabled=false
Atendimento__WhatsApp__Evolution__BaseUrl=
Atendimento__WhatsApp__Evolution__ApiKey=
Atendimento__WhatsApp__Evolution__InstanceName=

# SMTP
Smtp__Host=
Smtp__Port=587
Smtp__Username=
Smtp__Password=

# Primeiro administrador
BootstrapAdmin__Enabled=false
BootstrapAdmin__Email=
BootstrapAdmin__Password=
BootstrapAdmin__Name=

Para produção, mantenha os segredos nas variáveis de ambiente da plataforma de hospedagem.

Deploy no Render

O projeto possui Dockerfile pronto para publicação.

Fluxo básico:

crie um Web Service no Render;

conecte este repositório;

selecione deploy via Docker;

configure as variáveis de ambiente;

opcionalmente use /health como Health Check Path;

realize o deploy.

Health check:

GET /health

Resposta esperada:

{
  "status": "ok",
  "service": "CallMed"
}

Render / Linux

O projeto já inclui a correção utilizada para evitar excesso de FileSystemWatcher/inotify no Render:

DOTNET_USE_POLLING_FILE_WATCHER=1
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false

Mais detalhes em docs/deploy/V21.5.1-RENDER-HTTP500.md.

Integração com sistemas legados

A CallMed expõe uma API mínima para integração com HIS, agendas ou cadastros externos.

Exemplos:

GET  /api/integracao/v1/status
GET  /api/integracao/v1/pacientes/cpf/{cpf}
GET  /api/integracao/v1/disponibilidade?especialidade=...
POST /api/integracao/v1/solicitacoes

Documentação completa: docs/integracoes/INTEGRACAO-LEGADO.md.

Segurança

O projeto inclui, entre outras medidas:

ASP.NET Core Identity;

autorização por roles no servidor;

cookies HttpOnly e Secure em produção;

HTTPS e HSTS;

lockout após tentativas inválidas;

rate limiting para IA e webhooks;

segredos de integrações via configuração externa;

auditoria administrativa;

páginas autenticadas fora do cache persistente do PWA.

Documentação

Documento

Conteúdo

Arquitetura

Organização técnica e responsabilidades

Desafio SENAI

Relação entre o problema e a solução

Integração legada

API para sistemas existentes

Deploy / Render

Correção e configuração para Render

Play Store

Processo de publicação Android

Validação

Checklist técnico da release

Changelog V21.6

Reorganização da versão atual

Android / TWA

Geração e configuração do app Android

Contexto acadêmico

A CallMed foi desenvolvida a partir de um desafio acadêmico do SENAI voltado à melhoria do processo de marcação de consultas.

O projeto prioriza:

acessibilidade;

atendimento híbrido;

baixo custo;

padronização;

redução de tarefas manuais;

diminuição de absenteísmo;

melhor aproveitamento da agenda;

integração com estruturas existentes.

<p align="center">
  <strong>CallMed</strong><br>
  Todos os canais. Uma única agenda.<br>
  <sub>Agendamento mais rápido, acessível e conectado.</sub>
</p>
