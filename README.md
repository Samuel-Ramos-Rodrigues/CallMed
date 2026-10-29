

## Reestruturação completa do CSS (29/10/2026)

- Removidas as folhas CSS antigas e seus links duplicados.
- Criada uma única folha de estilos: `CallMedCrud/wwwroot/css/callmed.css`.
- Os layouts autenticado, público e de identidade carregam o CSS centralizado via `_DocumentHead.cshtml`.
- Páginas offline/contingência também usam a folha única.
- Service worker atualizado para armazenar somente o novo CSS e invalidar o cache anterior.
- Tema claro usa cinza-azulado com superfícies claras; tema escuro usa azul-marinho com superfícies profundas; o acento é índigo/lilás, sem depender do verde antigo.
- Botões têm cores explícitas para texto, hover, foco e desabilitado; formulários, tabelas, status, navegação, dashboard e atendimento possuem estilos de base.

**Validação pendente:** executar `dotnet build CallMed.sln` e fazer testes visuais de todas as rotas no navegador. A folha foi criada para substituir a camada visual antiga, mas módulos com markup muito específico ainda devem ser conferidos em execução.
