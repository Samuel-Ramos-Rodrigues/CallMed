# CallMed — novo layout e organização

Base: CallMed V22.2. Atualização visual: 24/09/2026.

## O que mudou

- Navegação superior no computador, menu de módulos por perfil e navegação inferior no celular.
- Painel da equipe com agenda, pendências, ocupação e ações rápidas.
- Área do paciente com próxima consulta, atalhos, solicitações e modo fácil.
- Página pública e login redesenhados, com azul-marinho, superfícies claras e detalhes em azul e pêssego.
- Formulários, tabelas, agenda, central de atendimento, assistente e páginas de conta com estilos compartilhados.
- Tema claro/escuro, preferências de acessibilidade e adaptação a telas pequenas.

## Limpeza dos arquivos

Foram removidos ou consolidados 23 arquivos:

| Grupo | Quantidade | Destino |
|---|---:|---|
| Camadas antigas de CSS | 5 | Substituídas pela base e pelos estilos de módulos |
| Changelogs separados por versão | 8 | `docs/CHANGELOG.md` |
| Relatórios antigos em TXT | 10 | `docs/validacao/HISTORICO.txt` |

As folhas de estilo passaram de 646.080 para 86.880 bytes (redução de aproximadamente 86,6%, sem minificação). Há seis arquivos CSS, divididos por responsabilidade. Os scripts e estilos usam versionamento de conteúdo nos layouts Razor, e o cache estático do service worker recebeu uma nova versão.

| Arquivo | Responsabilidade |
|---|---|
| `CallMedCrud/wwwroot/css/site.css` | Cores, tipografia, navegação, componentes, formulários e painéis |
| `CallMedCrud/wwwroot/css/modules.css` | Agenda, agendamento, atendimento, confirmações e assistente |
| `CallMedCrud/wwwroot/css/public.css` | Página pública e privacidade |
| `CallMedCrud/wwwroot/css/identity.css` | Login, cadastro e recuperação de acesso |
| `CallMedCrud/wwwroot/css/contingencia.css` | Atendimento em contingência |
| `CallMedCrud/wwwroot/css/comprovante.css` | Comprovantes e impressão |
| `CallMedCrud/Views/Shared/_DocumentHead.cshtml` | Metadados, preferências iniciais e CSS base |
| `CallMedCrud/Views/Shared/_Layout.cshtml` | Navegação da aplicação autenticada |
| `CallMedCrud/Views/Shared/_PublicLayout.cshtml` | Estrutura pública |
| `CallMedCrud/Views/Shared/_AuthLayout.cshtml` | Estrutura das páginas de autenticação |
| `CallMedCrud/wwwroot/js/preferences.js` | Preferências aplicadas antes da pintura da página |
| `CallMedCrud/wwwroot/js/shell.js` | Menu de módulos e menus de preferências/conta |
| `CallMedCrud/wwwroot/js/login.js` | Exibição e ocultação da senha |

Classes antigas ainda usadas pelos módulos foram mantidas para compatibilidade. Novas alterações de aparência devem entrar no arquivo responsável acima, sem criar outra camada de CSS por versão.

## Backend preservado

A comparação de conteúdo com a base confirmou que os 170 arquivos C# permaneceram idênticos. Controllers, modelos, serviços, autorização, integrações, migrações e regras de negócio não foram alterados nesta atualização. A identidade técnica `MKSANCrud` continua nos namespaces existentes.

Não é necessária uma migração adicional de banco por causa do layout. Para preparar o ambiente e executar as migrações já existentes, siga o README e `docs/ENTREGA-V22.md`.

## Verificações realizadas

| Verificação | Resultado |
|---|---|
| Compilação Release da solução com .NET SDK 8.0.414 | Aprovada, sem erros ou avisos |
| Verificações existentes em `tests/CallMed.Checks` | 11 aprovadas |
| Renderização de 15 telas/estados em 1440 e 390 px | 30 respostas HTTP 200; sem exceções JavaScript ou rolagem horizontal da página |
| Menu, foco, tema, modo fácil, senha, etapas do cadastro, convênio e seleção de horários | Aprovados |
| Painéis, login, cadastro, agendamento e cadastro de paciente em 320 px | Sem rolagem horizontal da página |
| Referências locais a CSS, JavaScript e imagens | Nenhum arquivo ausente encontrado |
| Comparação dos arquivos C# | 170 preservados |

A revisão visual usou as views Razor e os arquivos estáticos do projeto em um ambiente isolado com dados fictícios. Para renderizar login e cadastro nesse ambiente, apenas as cópias de teste receberam rotas absolutas. A seleção de médicos, datas e horários foi exercitada com respostas simuladas.

Essas verificações não equivalem à homologação com banco real, login real, gravação de consultas, envio de mensagens, Evolution, SMTP, SMS, IA, instalação PWA ou dispositivo Android. Nenhuma mensagem real foi enviada e nenhum ambiente de produção foi atualizado. O ambiente de testes visuais, SDK, navegador e dependências baixadas não fazem parte do ZIP.

Para repetir as verificações de código:

```sh
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release --no-restore
dotnet run --project tests/CallMed.Checks -c Release
```

Para executar a aplicação, configure o ambiente conforme o README e use:

```sh
dotnet run --project CallMedCrud
```

## Prévias

As imagens abaixo foram capturadas no navegador. Nomes, contagens e consultas são demonstrativos.

- [Painel da equipe](previas/Painel-equipe.png)
- [Página pública](previas/Inicio-publico.png)
- [Área do paciente no celular](previas/Area-paciente-mobile.png)
- [Login](previas/Login.png)

## Arquivos consolidados ou substituídos

- `CallMedCrud/wwwroot/css/design-v2163.css`
- `CallMedCrud/wwwroot/css/design-v2164.css`
- `CallMedCrud/wwwroot/css/design-v2165.css`
- `CallMedCrud/wwwroot/css/flow-v221.css`
- `CallMedCrud/wwwroot/css/shiftboard-v222.css`
- `docs/CHANGELOG-V21.6.1.md`
- `docs/CHANGELOG-V21.6.2.md`
- `docs/CHANGELOG-V21.6.3.md`
- `docs/CHANGELOG-V21.6.4.md`
- `docs/CHANGELOG-V21.6.5.md`
- `docs/CHANGELOG-V21.6.md`
- `docs/CHANGELOG-V22.1-DUALFLOW.md`
- `docs/CHANGELOG-V22.2-SHIFTBOARD.md`
- `docs/validacao/TESTES-CODIGO-V22.txt`
- `docs/validacao/TESTES-FILA-V22.txt`
- `docs/validacao/VALIDACAO-V21.6.1.txt`
- `docs/validacao/VALIDACAO-V21.6.2.txt`
- `docs/validacao/VALIDACAO-V21.6.3.txt`
- `docs/validacao/VALIDACAO-V21.6.4.txt`
- `docs/validacao/VALIDACAO-V21.6.5.txt`
- `docs/validacao/historico/VALIDACAO-V21.4.txt`
- `docs/validacao/historico/VALIDACAO-V21.5.1.txt`
- `docs/validacao/historico/VALIDACAO-V21.5.txt`
