# Arquitetura — auditoria contextual parcial

**Confirmado no código:** solução .NET 10, Web (ASP.NET MVC/Razor/Identity), Application, Persistence (EF Core + Npgsql), Domain, Worker opcional, projeto de testes e Android Kotlin/Compose.

**Pontos de entrada examinados:** `src/CallMed.Web/Program.cs`, `src/CallMed.Worker/Program.cs`, `ServiceCollectionExtensions.cs` (linhas 1–127), `CallMedJobMode.cs` e Dockerfiles.

**Fluxos gerais:** Web autentica e apresenta ações; Application contém serviços de agenda, comunicação, IA, jornada e integrações; Persistence coordena EF/migrations/outbox; Worker registra jobs quando `Jobs:Mode=external`; App Android consome `/api/mobile/v1`.

**Risco de operação:** site pode rodar jobs embutidos por padrão; Worker exige modo externo. Conferir as duas instâncias no Render antes de deploy, sem mudar produção.

**Qualidade da cobertura:** inventário de todos os arquivos concluído; auditoria contextual e de dependências ainda é parcial. Existem componentes e métodos não examinados manualmente. Consulte `cobertura-linhas.json`.

Veja `mapa-dependencias.md` para grafo confirmado de projetos.
