# Correção dos filtros e ações das listas — 31/10/2026

## Origem do problema
- Na tela de pacientes, o formulário `v16-search-form` herdava os mesmos estilos de painel que o próprio contêiner `patient-toolbar`, causando um cartão dentro do outro e a quebra dos botões.
- Os antigos menus `details.row-menu` tinham 34 × 34 px, mas continham vários links que não dispunham de área de layout; as ações podiam aparecer fora da célula e sobrepor linhas adjacentes.

## Correções
- Barra de pesquisa única e alinhada, com comportamento adaptativo no celular.
- Menus antigos substituídos por links de ação com tamanho definido e fluxo normal da célula, preservando os destinos de cada ação.
- Aplicado a Pacientes, Médicos, Consultas e Funcionários.
- Tabelas densas com largura mínima legível e rolagem horizontal restrita ao painel em telas estreitas; mensagem explicativa nas telas menores.
- Atualizado cache do PWA para incluir a folha de refinamento mais recente.

## Verificações executadas
- CSS validado com `tinycss2` e 14 scripts JavaScript verificados com `node --check`.
- Verificação estática das 81 páginas `.cshtml`: nenhum uso remanescente do menu problemático `row-menu`; quatro páginas atualizadas com `row-actions`.
- 20 cenários em Chromium: Pacientes, Médicos, Consultas e Funcionários em 390, 768, 1024, 1440 e 1784 px; sem sobreposição de botões ou rolagem horizontal do documento.
- Prévias em Chromium com estrutura HTML representativa e CSS real, não renderização ASP.NET conectada a banco de dados.
- Não foi possível compilar o .NET ou testar a navegação em produção neste ambiente.

## Recomendação de homologação
Verificar busca, filtros, edição, detalhes e desativação dos registros; conferir também a rolagem horizontal das listas no celular. Desativação deve continuar exigindo a confirmação já existente no fluxo do CallMed.
