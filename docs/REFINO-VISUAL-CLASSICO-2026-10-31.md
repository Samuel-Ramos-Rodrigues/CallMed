# CallMed · Refinamento visual clássico · 31 de outubro de 2026

Esta atualização **preserva o CallMed clássico** (índigo/azul, estrutura atual, logo original, Central em três colunas) e melhora o acabamento sem refazer a navegação ou alterar regras de negócio.

## Alterações
- Adicionada a folha de estilos `CallMedCrud/wwwroot/css/callmed-refino.css` como camada carregada **após** `callmed.css` em `Views/Shared/_DocumentHead.cshtml`.
- Central de Atendimento: legibilidade das conversas e mensagens, espaçamentos, estados ativos, campos de composição, rascunhos, alertas, ícones de canal e status dos canais.
- Componentes compartilhados: cabeçalhos, indicadores, formulários, botões, tabelas, painéis, cartões, popovers e modais.
- Agenda/agendamento: bordas, estados de seleção, blocos de horários e acessibilidade.
- Cadastros, equipe, relatórios, configurações, integrações e autenticação: refinamentos de componentes compartilhados.
- Mobile: campos de entrada com tamanho apropriado, tabelas com rolagem interna e ações de formulário sem sobreposição de campos.
- Ambos os temas claro/escuro herdam a mesma linguagem visual existente.

## Preservação da lógica
Nenhum controller, serviço C#, modelo, migração, integração (Gmail/Brevo/SMS/WhatsApp) ou JavaScript de negócios foi modificado. A lógica de IA por e-mail e aprovação de rascunho é exatamente a da base de origem.

## Validações disponíveis
- CSS analisado sem erro sintático pelo `tinycss2`.
- Prévia estática da Central em 390, 820 e 1440 px sem rolagem horizontal global; ícones dos canais mantidos.
- Prévia estática de pacientes/indicadores/tabela/formulário em 390 e 1440 px, nos temas claro e escuro; tabela em tela estreita rola dentro do painel.
- Arquivo ZIP verificado com `ZipFile.testzip`.

**Não foram executados compilação .NET 8, testes Razor autenticados ou integrações reais no Render.** As capturas são demonstrações estáticas, não capturas do sistema em execução.
