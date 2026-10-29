# Correção da paleta entre telas — 28/10/2026

## Problema observado

A área pública e o login forçavam variáveis claras por seletores próprios, enquanto a área autenticada respeitava o tema salvo. Alguns componentes de tabelas, cards, campos e páginas ainda tinham `background: #fff` fixo. Isso produzia tela de entrada clara com login/área autenticada escuros ou áreas brancas inesperadas.

## Correção

- Adicionada `wwwroot/css/clinic-unified-palette.css` como última folha dos layouts autenticado, público e de autenticação.
- Unificada a paleta de fundo, superfícies, bordas, texto, marca e alertas.
- A paleta clara usa fundo sálvia (`#dce6da`), superfície verde-marfim (`#e8eee4`) e superfície secundária (`#d4e1d2`), evitando branco puro como superfície dominante.
- A paleta escura aplica variáveis equivalentes à entrada pública, login e área autenticada, respeitando o tema escolhido.
- Cards, tabelas, cabeçalhos de tabela, campos, menus suspensos e componentes legados passam a usar variáveis de tema no lugar de cores brancas fixas.
- Alto contraste foi mantido como modo separado, monocromático e prioritário.
- Atualizado o cache do service worker para `callmed-static-v35-unified-clinical-theme`, de modo que o PWA solicite os estilos novos.
- A cor do navegador/instalação foi alinhada ao fundo sálvia claro e ao verde-escuro do tema escuro.

## Validação

- Arquivo CSS analisado com parser CSS; sem erros de sintaxe.
- Verificadas referências do arquivo CSS nos três layouts e no cache estático do service worker.
- JSON/XML/JavaScript e compilação .NET devem continuar sendo validados no processo normal de build/deploy; a compilação do backend não foi executada nesta entrega.

## Implantação

Enviar os arquivos ao repositório usado pelo Render e publicar uma nova versão. Após o deploy, recarregar o navegador e reabrir o PWA. O tema escolhido pelo usuário continua sendo respeitado; a diferença é que todas as telas agora usam os mesmos valores para esse tema.
