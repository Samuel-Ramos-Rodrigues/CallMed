# CallMed — reconstrução visual global

**Data:** 29/10/2026  
**Base funcional:** `CallMed-cadastro-termos-senha-forte(1).zip`  
**Fonte de estilos atual:** `CallMedCrud/wwwroot/css/callmed.css`

## Intenção do redesign

O CSS anterior foi usado somente para identificar a estrutura dos componentes e os seletores ainda presentes nas views Razor. A apresentação foi refeita com uma direção visual nova — índigo, safira, lavanda e neutros azulados — sem carregar as regras visuais antigas em cascata.

O modo claro utiliza fundos frios muito suaves, superfícies claras e texto azul-marinho. O modo escuro utiliza superfícies em azul profundo e texto de alto contraste. O verde não é mais a cor de marca; tons de verde ficam restritos aos estados semânticos de sucesso.

## Mudanças estruturais

- As 13 folhas de estilo anteriores foram substituídas por uma única folha: `wwwroot/css/callmed.css`.
- Os três layouts Razor usam a folha através de `_DocumentHead.cshtml`.
- As páginas especiais de contingência, offline e comprovante deixaram de incluir folhas antigas separadas; seus componentes e estilos de impressão agora estão na folha unificada.
- O service worker passou a pré-carregar somente `/css/callmed.css` e teve o identificador de cache atualizado para `callmed-static-v39-visual-system-rebuilt`.
- `theme.js`, o meta theme-color e `manifest.json` foram atualizados para a paleta nova.
- O CSS usa os nomes de classes que as views atuais já produzem, para evitar alterações desnecessárias de rotas, formulários ou lógica de negócio.

## Áreas abrangidas pela folha unificada

1. Shell autenticado: cabeçalho, navegação, menu de módulos, menus de preferências/conta e navegação móvel.
2. Painel da equipe: hero, acesso rápido, agenda, indicadores, pontos de atenção e ocupação.
3. Atendimento omnichannel: filtros, inbox, conversa, mensagens recebidas/enviadas, rascunhos, falhas de entrega, composição de resposta, dados do contato e diálogo de estado dos canais.
4. Agenda e agendamento: filtros, comutação dia/semana/lista, calendário, células de horários, vagas e etapas de seleção.
5. Cadastro e manutenção: pacientes, médicos, funcionários, especialidades, convênios, acessos e disponibilidade.
6. Solicitações, triagem, lista de espera, exames, auditoria, relatórios, integrações, configurações e conta.
7. Site público, página inicial do paciente, páginas legais, login, cadastro, senha forte, consentimento dos termos e etapas de cadastro.
8. Estados vazios, alertas, badges de status, campos, tabelas, popovers, acessibilidade, responsividade, contingência/offline e impressão.

## Regras para contraste e estados

- Botões primários, secundários, claros e de perigo definem explicitamente fundo e cor de texto.
- Estados hover, active, focus-visible e disabled possuem cores próprias. O estado disabled não usa opacidade global que apague o texto.
- Campos definem cores de texto, placeholder, borda, foco, opção de seleção e erro.
- Cartões de indicadores mantêm cor de texto própria, independentemente do fundo.
- A paleta é controlada por tokens e `[data-theme="dark"]`, compatível com o seletor já existente.
- Há regras para foco visível, movimento reduzido, alto contraste, texto ampliado e navegação móvel.

## Validações realizadas neste pacote

- Foram verificadas referências CSS nos layouts/views e nas páginas offline: todas apontam para o arquivo existente `callmed.css`.
- Não foram encontradas referências executáveis às folhas antigas removidas.
- A folha CSS passou por análise sintática (853 regras de topo, zero erros de parsing reportados pelo parser utilizado).
- `manifest.json` passou pela validação JSON.
- Os arquivos não CSS do ZIP de origem estão preservados; as mudanças de código fora da folha visual são limitadas aos caminhos de estilo, cache e cores de tema.

## Limitações conhecidas

O SDK .NET não está disponível neste ambiente, portanto não foi possível executar `dotnet build` nem validar as views Razor em execução. A validação estática não substitui um teste visual de cada página, perfil e estado real no navegador. Antes de publicar, abrir a solução no Visual Studio, compilar, testar tema claro/escuro e conferir pelo menos painel, atendimento, agenda, cadastro, autenticação e estados de erro/disabled.

As revisões CSS anteriores neste repositório são registros históricos de versões anteriores; para a estrutura visual atual, este documento e `wwwroot/css/callmed.css` são a referência.
