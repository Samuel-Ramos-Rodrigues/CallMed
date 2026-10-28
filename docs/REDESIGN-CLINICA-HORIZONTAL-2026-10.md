# Redesign clínico horizontal — outubro de 2026

## Direção visual
- Navegação horizontal em duas faixas no desktop, sem sidebar fixa.
- Fundo verde-sálvia/cinza suave, superfícies marfim esverdeado, tipografia grafite e acentos verde-petróleo.
- Dashboard reorganizado com saudação, atalhos, próximos atendimentos, resumo do dia, pontos de atenção e ocupação.
- Componentes compartilhados mantêm estilo coeso em cadastros, consultas, exames, atendimento, relatórios e telas públicas.
- Alertas, validações, estados de status, foco e hover padronizados.
- Em telas estreitas, a navegação horizontal vira navegação inferior já existente e o dashboard se empilha.

## Notas de funcionalidade
- O dashboard usa dados do `FuncionarioPainelViewModel`; não foram criados pacientes, consultas ou notificações fictícias.
- A triagem exibida no atalho é a triagem administrativa de exames pendentes, não a triagem de consultas.
- O cache do service worker foi atualizado para invalidar estilos antigos no próximo carregamento.

## Validação
Foram feitas verificações estáticas de Razor/HTML estrutural, CSS, JSON e referências de recursos. A compilação .NET deve ser executada em ambiente com SDK .NET 8 e a interface precisa ser homologada no navegador do ambiente de publicação.
