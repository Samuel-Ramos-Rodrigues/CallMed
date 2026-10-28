# Revisão visual CallMed — 28/10/2026

## O que mudou

- Removida a barra flutuante de acessibilidade do canto superior da tela de login. Os controles de acessibilidade permanecem na área autenticada. Preferências previamente salvas continuam sendo aplicadas.
- Definida uma identidade clínica refinada em sálvia, verde-petróleo e superfícies marfim-esverdeadas, evitando branco puro como fundo dominante e corrigindo textos de baixo contraste.
- Aplicada uma camada de estilos compartilhada (`wwwroot/css/clinic-polish-2026.css`) em páginas autenticadas, públicas e de autenticação.
- Restaurada a navegação horizontal no cabeçalho da área autenticada, sem barra lateral fixa, e ajustado o comportamento responsivo para telas médias e móveis.
- Normalizadas cores de painéis, formulários, tabelas, rótulos, alertas, notificações e estados de consulta. O tema escuro explícito mantém cores pareadas para leitura.
- A página pública e as páginas de autenticação usam o tema claro clínico para evitar texto claro sobre fundo claro/dark accidental causado pela preferência de sistema antiga.
- O tema padrão para novas sessões é claro; uma escolha explícita de tema é mantida. Preferências antigas `system` não marcadas como escolha explícita migram para `light`.
- Incrementado o cache do service worker e adicionada a nova folha de estilos aos recursos estáticos.

## Validação

Foram feitas verificações estáticas da estrutura CSS, JSON/XML e sintaxe JavaScript. A compilação .NET e a inspeção completa em navegador precisam ser confirmadas no ambiente de implantação.
