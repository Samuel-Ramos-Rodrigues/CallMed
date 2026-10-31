# Correção visual do CallMed: recuperação de cobertura CSS

## Causa
A substituição anterior do CSS (aprox. 59 KB / 562 regras) suprimiu classes existentes nas Razor. A comparação estática apontou 195 classes de uso real que possuíam seletores na versão completa e ficaram ausentes na folha reescrita. Isso atingiu vários módulos, incluindo agendamento, autenticação, painéis médicos e componentes de atendimento.

## Correção
- Restaurada a folha clássica completa (anterior à reconstrução incompleta).
- Incorporados no MESMO `callmed.css` os refinamentos existentes para Central, formulários, filtros e tabelas.
- Mantidas ações de tabela no fluxo de layout, sem menus sobrepostos.
- Correção de toolbar de paciente, overflow das tabelas e ajustes mobile.
- Atualizada a versão do cache do Service Worker para descartar o CSS incompleto.
- Uma única folha de estilos sem `@import`, sem camadas CSS extras no layout.

## Limites
Validações estáticas e testes visuais em páginas representativas não substituem o build .NET e testes de navegação em todas as páginas reais com autenticação e banco. Nada em controllers, serviços ou migrations foi modificado nesta correção.
