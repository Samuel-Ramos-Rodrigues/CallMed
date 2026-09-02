# Validação — CallMed V21.6

Validação estática executada após a reorganização estrutural.

## Resultado

- JSON (`appsettings.json` e `manifest.json`): válido.
- JavaScript + Service Worker: `node --check` sem erros.
- CSS: parser `tinycss2` sem erros.
- Service Worker: 21/21 assets locais encontrados.
- Controllers: somente classes `*Controller`, uma por arquivo.
- ViewModels: um tipo público por arquivo.
- Entidades: um tipo público por arquivo.
- DTOs e Options separados de Controllers/Models/Services.
- Referências antigas `MKSANCrud.Models.ViewModels`: 0.
- Referências antigas `MKSANCrud.Models.Agente`: 0.
- `asp-append-version`: 0; correção de FileSystemWatcher do Render preservada.
- `MKSANContext`: 1 declaração.
- `Usuario`: 1 declaração.
- EF Core: 16 `DbSet`s e 16 tipos cobertos por `IEntityTypeConfiguration`.
- Docker: polling de file watcher e reload de configuração desabilitado permanecem configurados.
- TWA: `versionCode 2160`, `versionName 21.6.0`.
- Cache PWA: `callmed-static-v21-6-organized`.

## Limitação do ambiente

O SDK do .NET não está instalado no ambiente de geração, portanto `dotnet build` não pôde ser executado aqui. A revisão utilizou validação estrutural/sintática estática e preservação dos tipos/registros existentes.
