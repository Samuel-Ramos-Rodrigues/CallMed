# Auditoria de compatibilidade visual — CallMed

**Data:** 29/10/2026  
**Base analisada:** `CallMed-assistente-flutuante-corrigido-2026-10-29.zip`  
**Arquivo visual central:** `wwwroot/css/callmed.css`

## 1. Escopo da auditoria

A revisão cruzou a folha CSS com:

- 73 views Razor em `Views/`;
- 7 páginas Razor em `Areas/Identity/Pages/`;
- 14 arquivos JavaScript em `wwwroot/js/`;
- layouts compartilhados, cabeçalho, navegação, popovers, assistente flutuante, páginas offline e de contingência;
- classes estáticas e classes alternadas/criadas por JavaScript.

A aplicação carrega uma única folha de estilo nos layouts (`Views/Shared/_DocumentHead.cshtml`). As páginas estáticas de contingência e offline apontam para o mesmo arquivo. O service worker também pré-carrega `callmed.css`.

## 2. Problemas encontrados e corrigidos

### Painel do médico — indicadores

A marcação real dos KPIs contém um `div` interno com rótulo, número e descrição. Regras antigas só formatavam `span` e `strong` como filhos diretos do cartão, deixando o conteúdo interno sem hierarquia. O conteúdo agora usa uma grade interna, número em bloco e descrição separada; o ícone recebe cor semântica.

### Cadastro de médicos e pacientes

As seções dos formulários estavam recebendo borda/fundo de cartão dentro de outro cartão, criando cartões aninhados e espaçamento inconsistente. Os formulários agora usam um único contêiner visual, com as seções internas transparentes e divisórias próprias.

### Exceções da agenda

O campo de horário é um `.field` com rótulo, seletor e mensagem de validação. A regra `.exception-time` o transformava em uma grade de duas colunas, podendo posicionar rótulo e seletor lado a lado. Agora é uma coluna flexível, com rótulo acima do controle.

A view gera as classes dinâmicas `type-encaixe`, `type-bloqueio` e `type-ausencia`. O CSS anterior usava `.block` e `.away`, que não correspondiam ao HTML gerado. Os três estados agora possuem cores e fundos próprios; os ícones de seleção também diferenciam encaixe, bloqueio e ausência.

### Consultas futuras do painel médico

O horário `.v16-time` não tinha regra própria e aparecia como texto sem destaque na linha da consulta. Agora é apresentado como chip de horário, com largura mínima, alinhamento e números tabulares; em telas estreitas ele se alinha à direita.

### Menu de confirmações

O popover combinava `.v18-popover` e `.cm-confirm-popover`. A regra genérica de popover tinha especificidade suficiente para reduzir a bandeja de confirmações de 360 px para 290 px. A regra específica agora mantém 360 px no desktop e limita a largura ao viewport no celular. Também foi removido o deslocamento antigo de `-52px`, que podia empurrar a bandeja para fora da tela.

### Controles e estados dinâmicos

Foram conferidos e ajustados:

- switches de profissional ativo e de dias da agenda, com foco visível e estado marcado;
- campo de assunto de e-mail, oculto fora do canal de e-mail e visível quando o JavaScript ativa `.is-email`;
- cartões de estado dos canais de atendimento (`.channel-status-card`, `.is-ok`, `.is-off`);
- campos de data divididos em dia, mês e ano;
- estados do chat flutuante, caixa de mensagens, campo de digitação e botão de envio;
- foco, hover e desabilitado dos botões.

### Página de convênios

O único estilo inline estático encontrado nessa página foi movido para a classe `.cm21-rules-actions`. Os demais estilos inline localizados no projeto são valores calculados em tempo de execução (por exemplo, largura de progresso ou contagem de dias) e foram preservados por dependerem dos dados.

## 3. Compatibilidade das classes e tokens

A comparação com as 13 folhas CSS da versão histórica encontrou 143 classes antigas que não existem mais na folha atual. Nenhuma dessas classes removidas é referenciada pelas views Razor ou pelos scripts atuais; portanto, não foi necessário reintroduzir o CSS obsoleto.

Foram verificadas 64 variáveis CSS definidas e 57 variáveis referenciadas. **Não há referências `var(--...)` sem definição.** O CSS contém 1.531 regras qualificadas e 5.071 declarações; o parser CSS não apontou erros de sintaxe nas declarações.

Algumas classes do HTML são modificadores, nomes semânticos ou classes dinâmicas (por exemplo, `channel-*`, `status-*`, `cm25-*` em conjunto com uma classe-base). Elas não precisam ter uma regra isolada quando o estilo é fornecido pela classe-base ou por seletores de atributo/estado. A revisão identificou e corrigiu os casos em que o nome realmente gerado não correspondia ao seletor.

## 4. Testes de navegador executados

Foram usadas prévias HTML de teste com fragmentos de marcação reais do projeto e a folha CSS atual, executadas em Chromium/Playwright. Isso testa os estilos e o posicionamento dos componentes reproduzidos, mas não equivale a executar a aplicação Razor completa.

Matriz executada:

| Tema | Desktop | Celular |
|---|---:|---:|
| Claro | 1440 px | 390 px |
| Escuro | 1440 px | 390 px |

Resultados:

- largura do documento permaneceu igual à largura do viewport nos quatro cenários;
- bandeja de confirmações mediu 360 px no desktop e ficou dentro dos limites do viewport no celular;
- rótulo e seletor do campo de horário ficaram empilhados;
- KPIs mantiveram rótulo, número e descrição separados;
- cartões internos dos formulários não duplicaram o fundo do cartão externo;
- campo de assunto foi ocultado/mostrado conforme o estado do canal;
- os três tipos de exceção receberam cores distintas nos dois temas;
- matriz semanal de disponibilidades mantém rolagem horizontal dentro do seu próprio contêiner, sem alargar o documento.

### Contraste medido dos botões

Relações de contraste entre texto e fundo, medidas na prévia do Chromium:

| Estado | Claro | Escuro |
|---|---:|---:|
| Botão principal | 6,41:1 | 6,65:1 |
| Botão secundário | 11,08:1 | 13,09:1 |
| Botão desabilitado | 5,82:1 | 8,56:1 |

O hover do botão principal também passou no teste de contraste mínimo de 4,5:1. Os valores medidos ficam acima do critério WCAG AA para texto normal.

## 5. Cache e arquivos alterados

O cache do service worker foi atualizado para `callmed-static-v43-css-compat-audit`, para que os clientes solicitem a folha revisada.

Em relação ao ZIP-base, as alterações de código limitam-se a:

1. `CallMedCrud/wwwroot/css/callmed.css` — ajustes e correções de compatibilidade visual;
2. `CallMedCrud/wwwroot/service-worker.js` — atualização da chave de cache;
3. `CallMedCrud/Views/Convenios/Index.cshtml` — remoção de um estilo inline fixo e uso de classe CSS.

Este relatório foi acrescentado em `CallMedCrud/docs/`.

## 6. Limitações da validação

O ambiente desta revisão não possui o SDK .NET (`dotnet` não está instalado). Por isso, **não foi possível compilar a solução, executar o servidor ASP.NET nem validar fluxos autenticados e dados reais de cada tela**. Os testes de navegador cobrem componentes representativos com HTML reproduzido a partir das views, não todos os estados de todas as rotas.

Antes de publicar, abra `CallMed.sln` no Visual Studio, compile e percorra os módulos com contas de administrador, médico, funcionário e paciente. O objetivo desta entrega é eliminar incompatibilidades visuais comprovadas sem alterar rotas, controllers, lógica de negócio ou scripts de atendimento.
