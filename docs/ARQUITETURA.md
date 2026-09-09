# Arquitetura do CallMed

A V21.6 reorganiza a solução sem alterar o domínio público do sistema ou os nomes de tabelas/migrations existentes.

## Aplicação web

```text
CallMedCrud/
├── Controllers/
│   ├── Admin/          # configurações, auditoria e integrações administrativas
│   ├── Agenda/         # agenda, consulta, disponibilidades e exceções
│   ├── Api/            # webhooks e integração com sistemas legados
│   ├── Atendimento/    # central, IA, confirmações, solicitações e lista de espera
│   ├── Cadastros/      # pacientes, médicos, funcionários, convênios e especialidades
│   ├── Conta/          # área do paciente
│   └── Paineis/        # home, dashboards e relatórios
├── Data/
│   ├── Configurations/ # mapeamentos do Entity Framework Core
│   ├── Identity/       # usuário do ASP.NET Identity
│   └── MKSANContext.cs
├── DTOs/
│   ├── Agente/         # contratos de entrada/saída do assistente
│   ├── Atendimento/    # mensagens e resultado de canais
│   └── Integracao/     # contratos da API legada
├── Extensions/         # registro de DI, middleware e endpoints
├── Middleware/         # middlewares HTTP da aplicação
├── Models/
│   ├── Constants/      # status e valores de domínio baseados em string
│   ├── Entities/       # entidades persistidas pelo EF Core
│   └── Enums/          # enumerações do domínio
├── Options/            # POCOs de configuração vindos de appsettings/variáveis
├── Services/
│   ├── Agendamento/
│   ├── Agente/
│   ├── Atendimento/
│   ├── Clinica/
│   ├── Database/
│   ├── Email/
│   ├── Startup/
│   └── Usuarios/
├── ViewModels/         # modelos exclusivos da interface MVC
├── Views/
└── wwwroot/
```

## Regras de organização

- **Controllers** contêm apenas controllers. DTOs não ficam dentro deles.
- **Entities** representam dados persistidos no banco.
- **ViewModels** existem apenas para composição/validação de telas.
- **DTOs** representam contratos entre HTTP, integrações, IA e canais.
- **Options** representam configuração e não regra de negócio.
- **Services** concentram regras e integrações reutilizáveis.
- **Data/Configurations** concentra mapeamento EF; `MKSANContext` apenas expõe os `DbSet`s e aplica as configurações.
- Interfaces de serviço ficam em subpastas `Contracts` e serviços em segundo plano em `Background`.

## Compatibilidade

Os namespaces das entidades EF (`MKSANCrud.Models` e `MKSANCrud.Models.Atendimento`) e do usuário Identity (`MKSANCrud.Data.Usuario`) foram preservados para evitar mudanças artificiais no snapshot de migrations e no banco Neon existente.
