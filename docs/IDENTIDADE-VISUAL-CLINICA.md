# Identidade visual CallMed — sistema atualizado

## Direção visual

- Paleta de marca nova: índigo e safira, com lavanda como acento e neutros azulados.
- Modo claro: fundo azul-acinzentado suave, cartões claros e texto escuro com contraste alto.
- Modo escuro: fundo azul-marinho profundo, superfícies separadas por tons de azul e tipografia clara.
- Verde não é usado como cor primária de marca; é reservado para estados semânticos como sucesso/confirmado.
- Estilo mais editorial e organizado: espaços generosos, raios consistentes, bordas sutis e elevação discreta.

## Arquitetura

- `CallMedCrud/wwwroot/css/callmed.css`: folha única e fonte visual atual.
- `CallMedCrud/Views/Shared/_DocumentHead.cshtml`: inclusão compartilhada.
- `CallMedCrud/wwwroot/js/preferences.js` e `theme.js`: aplicação e persistência da preferência visual.
- `CallMedCrud/wwwroot/manifest.json`: cores da instalação PWA.
- `CallMedCrud/wwwroot/service-worker.js`: cache versionado para atualizar a aparência instalada.

O CSS antigo foi usado como referência funcional de seletores e estrutura das telas; suas regras visuais não são importadas. A folha nova cobre painel, atendimento, agenda, cadastro, relatórios, autenticação, conteúdo público, contingência/offline, estados de botões, acessibilidade e impressão.

## Validação

A checagem estática confirma referências de CSS válidas, um único arquivo CSS de produção, sintaxe CSS parseável e manifesto JSON válido. A compilação da solução e a inspeção de todas as páginas em execução continuam necessárias no Visual Studio, pois o SDK .NET não estava disponível no ambiente de reconstrução.
