# CallMed V21.6 — organização estrutural

- Controllers separados por responsabilidade, mantendo as rotas existentes.
- DTO de integração removido de dentro do controller e colocado em `DTOs/Integracao`.
- DTOs do agente e atendimento separados dos Models e Services.
- Options de Gemini, SMTP, Evolution, SMS e e-mail centralizados em `Options`.
- Entidades EF organizadas em `Models/Entities` sem trocar seus namespaces persistidos.
- Enums e constantes de domínio separados das entidades.
- ViewModels movidos para pasta própria e separados em um tipo público por arquivo.
- Interfaces de serviços organizadas em `Contracts`.
- Hosted services organizados em `Background`.
- Initializers do banco organizados em `Services/Database/Initializers`.
- `MKSANContext` reduzido e mapeamentos EF movidos para `Data/Configurations`.
- `Program.cs` reduzido; DI, pipeline e endpoints movidos para extensions e inicialização de startup.
- Documentação separada por arquitetura, deploy, integrações e validação.
- `.editorconfig` adicionado para manter o padrão do código.
- PWA/TWA atualizado para 21.6.0.
