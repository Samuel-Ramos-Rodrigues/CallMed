# CallMed — CSS único reescrito do zero

Data: 31/10/2026

## Objetivo
Manter o visual clássico do CallMed (paleta escura índigo, logo real, Central em três colunas), substituindo os estilos anteriores por uma única folha, limpa e organizada.

## Arquitetura
- Apenas `CallMedCrud/wwwroot/css/callmed.css` (criado do zero; sem `@import`).
- Removido `CallMedCrud/wwwroot/css/callmed-refino.css`.
- `_DocumentHead.cshtml`: somente uma referência CSS, com versionamento de arquivo.
- `service-worker.js`: remove a folha obsoleta da lista de cache e incrementa o nome da versão.
- Páginas Razor, JavaScript da Central, controllers, serviços e dados não foram alterados.

## Seções do novo arquivo
Tokens de cor claro/escuro, resets, shell/navegação, botões/formulários, cards/tabelas, cadastros e módulos, agenda, Central de Atendimento, login/público, paciente/médico e responsive/print.

## Decisões para evitar os defeitos anteriores
- Tabelas com rolagem dentro do próprio contêiner no celular; ações ficam dentro das células e não usam posicionamento absoluto.
- Barras de filtro com formulário flexível sem card aninhado.
- Central em três colunas no desktop, com rolagem independente da lista e do histórico; celular sem altura fixa e com rolagem natural.
- Modal limitado à viewport; controles podem quebrar a linha sem sobreposição.
- Barra de ações de formulários deixa de ser sticky em celular.
- Campos de entrada >=16px no celular para evitar zoom automático no iOS.
- O CSS não altera regras de negócio, autenticação nem integração Gmail/Brevo/IA.

## Limites de teste
A verificação estática e as prévias HTML/Chromium não equivalem a executar todas as páginas Razor com .NET, banco e autenticação; o build .NET e os testes reais em homologação permanecem necessários.
