# Correção de contraste e tela de login — outubro de 2026

## Problemas corrigidos

- Em tema escuro, havia superfícies que continuavam brancas enquanto textos usavam variáveis claras; agora cartões, tabelas, filtros, chips e campos acompanham o tema e os textos usam cores de contraste apropriadas.
- Métricas nos cartões de consulta usavam texto escuro fixo; agora usam as variáveis do tema.
- O modo de alto contraste alterava somente parte das variáveis e não neutralizava fundos/cores fixos de folhas antigas. A nova folha `clinic-final-fixes.css` inclui regras de alto contraste claro e escuro para tabelas, formulários, cartões, botões, alertas e navegação.
- Os controles de acessibilidade estavam disponíveis somente em algumas páginas/para pacientes. Agora o menu de acessibilidade também aparece no layout autenticado de todos os perfis e no layout de autenticação antes do login.
- A tela de login recebeu cartão mais legível, paleta clínica coerente, logo sem placa de fundo e controles de texto maior/alto contraste.

## Como testar

1. Faça login e abra **Minhas consultas** com aparência clara e escura. Verifique cabeçalho, cartões numéricos, filtro e tabela.
2. No botão `Aa`, ative **Alto contraste**: os fundos devem ficar monocromáticos e bordas/textos devem ficar nítidos. Teste também com o tema escuro selecionado.
3. Saia da conta e use os controles de acessibilidade na tela de login. A preferência é salva no navegador e persiste entre telas.
4. Teste o formulário com Tab, Enter e foco do teclado.

A nova folha de estilo está incluída na lista estática do service worker, com nome de cache atualizado para forçar a aquisição dos estilos novos.
