# Testar o CallMed

Execute os comandos abaixo a partir da pasta que contém CallMed.sln.

| O que verificar | Comando |
|---|---|
| Dependências entre projetos e estrutura dos módulos | `python scripts/check-architecture.py` |
| CSS, referência nas páginas offline e contrato de interface | `python scripts/check-css.py` e `node tests/css-integrity.cjs` |
| Compilação dos cinco projetos e do projeto de testes | `dotnet build CallMed.sln -c Release` |
| Testes gerais do backend | `dotnet run --project tests/CallMed.Checks -c Release` |
| Fluxos da agenda | `dotnet run --project tests/CallMed.Checks -c Release -- --agenda-fluxos` |
| API do Android | `dotnet run --project tests/CallMed.Checks -c Release -- --mobile` |
| Arquitetura e autorização | `dotnet run --project tests/CallMed.Checks -c Release -- --architecture` |
| Salas | `dotnet run --project tests/CallMed.Checks -c Release -- --salas` |
| Contraste da Home no navegador | `python tests/browser/regressoes/check-r9312-public-home.py` com Playwright e Chromium instalados. |

O [projeto .NET de testes](CallMed.Checks) agrupa os casos por área. O [ponto de entrada](CallMed.Checks/Program.cs) lista as opções de execução. As suítes PostgreSQL usam CALLMED_TEST_DB com um banco local isolado cujo nome começa com callmed_test. Elas podem recriar os dados desse banco.

Os [testes de navegador](browser/README.md) incluem uma fixture isolada. Os [cenários de regressão visual](browser/regressoes) cobrem versões e fluxos específicos. Os testes do aplicativo ficam junto do [projeto Android](../mobile/android/README-ANDROID.md).
