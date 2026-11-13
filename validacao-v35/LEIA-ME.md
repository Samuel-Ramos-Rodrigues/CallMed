# Validação da CallMed V35

Base de comparação: projeto V34, revisão 47. A entrega foi conferida com dados fictícios e provedores externos desativados.

| Verificação | Resultado |
|---|---|
| Compilação da solução, Release | Cinco projetos de produção e projeto de verificações; zero avisos e zero erros. |
| Backend | 14 suítes, 1.025 asserções aprovadas. |
| PostgreSQL 17 | Seis suítes, 51 asserções aprovadas, incluindo migração e concorrência. |
| Prompt administrativo | 12 comparações de saída idênticas à V34, incluindo SMS e valores com marcadores literais. |
| httpSMS | Cinco arquivos centrais idênticos à base por SHA-256; configurações existentes preservadas. |
| Interface | 47 páginas × quatro composições = 188 verificações; zero violações apontadas pelo axe no escopo escolhido e zero transbordamentos horizontais. |
| Rechecagem localizada | 12 verificações de Central, conta e agendamento após o último ajuste do compositor; zero observações. |
| Referências | 106 páginas Razor, 49 controllers, 371 referências MVC e 53 recursos estáticos; zero referências quebradas. |
| JavaScript | Sintaxe dos 33 arquivos de aplicação/verificação e do roteiro local conferida. |
| NuGet | Nenhuma vulnerabilidade reportada para o grafo consultado. |
| Publicação | Site e worker publicados. Inicialização real em Production, com PostgreSQL local, schema 37 e worker externo. |

## Interface

As combinações foram: 1366 px/escuro/100%, 1366 px/claro/100%, 390 px/escuro/100% e 390 px/claro/200%. A ferramenta usa o conjunto WCAG 2 A/AA e 2.1 AA do axe-core. O consolidado está em `interface-results.json`; o roteiro reproduzível fica em `tests/browser/review.html`, servido somente pela fixture de testes.

A conferência funcional no navegador verificou filtro direto do WhatsApp, abertura dos filtros avançados, fechamento com Esc, busca por função com Ctrl+K, acesso ao gerenciamento de salas, abas da conta, prévia de tema e seleção de área/profissional no agendamento. Na fixture, a busca de primeira vaga retornou ausência de horários e apresentou a mensagem correspondente. Não foi registrada uma consulta por esse percurso visual.

Os oito scripts de regressão Playwright existentes continuam no workflow de CI. Eles não foram executados nesta revisão local; a verificação visual desta entrega foi feita pelo navegador de revisão com o roteiro de 47 páginas.

## Compatibilidade e inicialização

`runtime-smoke.json` registra a inicialização do banco, do worker e do site, saúde/prontidão, proteção da API mobile, ausência das rotas da fixture e das telas Identity padrão, redirecionamento do link antigo de redefinição e igualdade da folha CSS publicada com a origem.

O APK e o código Android foram mantidos; somente a documentação nativa foi atualizada. Não houve recompilação ou teste físico do Android. O projeto mantém os mapeamentos de builds nativos anteriores porque ainda podem ser necessários para interpretar falhas dessas versões.

`alteracoes-arquivos.json` compara os arquivos com a base. Transferências exatas são identificadas por hash; mudanças de responsabilidade e extrações são descritas em `docs/ARQUITETURA.md`. O manifesto não inclui seus próprios relatórios nem os diretórios de compilação/ferramentas.

## Limites

Não houve publicação no Render, envio real por httpSMS/WhatsApp/e-mail, chamada entre dispositivos/redes ou uso de dados de pacientes. O teste automatizado de acessibilidade não equivale a certificação WCAG e não substitui revisão com tecnologias assistivas. Também não constitui certificação LGPD ou regulatória. Confira provedores, permissões e o fluxo completo no ambiente de homologação da clínica antes de usar em produção.
