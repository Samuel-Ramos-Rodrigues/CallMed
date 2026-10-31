# CallMed — CSS recriado do zero

Data: 31/10/2026

## O que mudou

O arquivo `CallMedCrud/wwwroot/css/callmed.css` foi completamente substituído.
O código CSS anterior **não** é importado ou referenciado pelo novo CSS.

A linguagem visual adotada é **Aurora Clínica**: azul hospitalar, turquesa e
superfícies limpas; ela usa a logo que já estava presente no projeto
(`wwwroot/images/logo-callmed-mark.png` e o logo horizontal). Não foram geradas
novas marcas.

### Cobertura

- Navegação horizontal do layout compartilhado, menu de módulos e barra móvel;
- Sistema de cores claro/escuro, espaçamento, tipografia, alertas, formulários,
  botões, estados e indicadores;
- Início da equipe/admin, Central de Atendimento, conversas e rascunhos;
- Agenda, agendamento em etapas, disponibilidade e exceções;
- Portal do paciente, área do médico e páginas de cadastro;
- Acesso, cadastro e recuperação de senha; site público;
- Relatórios, integrações, configurações e acessos;
- Lista de solicitações, cartões e confirmações;
- Assistente flutuante, responsividade, acessibilidade e impressão.

### Preservação

Nenhuma view Razor, controller, serviço, migração, JavaScript, segredo ou
configuração de backend foi alterado. As funções de Gmail API, Brevo SMTP,
whatsApp, SMS e atendimento por IA permanecem exatamente como estavam na base.

### Verificações executadas

- Parser CSS: sem erros de sintaxe no novo arquivo.
- Prévia estática no Chromium, com largura 1440 e 390 pixels, temas claro e
  escuro: nenhum transbordamento horizontal na tela de Atendimento simulada.
- Integridade da estrutura e do ZIP: verificada no empacotamento.

### Verificações pendentes antes de produção

Não foi possível fazer `dotnet build`, pois o SDK .NET 8 não está instalado
neste ambiente. As capturas de tela são uma **prévia estática do CSS**, não
execução do servidor com banco de dados.

Recomenda-se conferir em homologação as páginas de agenda semanal, formulários
com validação, cadastro do paciente, diálogos/popovers e o fluxo de envio de
rascunhos antes de publicar no Render.

### Voltar ao CSS anterior

O ZIP original do projeto (antes desta alteração) contém a folha de estilo
anterior. Basta restaurar esse arquivo em `CallMedCrud/wwwroot/css/callmed.css`.
