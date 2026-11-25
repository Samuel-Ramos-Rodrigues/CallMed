# CallMed Clinic R9.1 — Controles, perfil e acessibilidade

Base: CallMed Clinic R9.1 compatível exclusivamente com o projeto R9 completo.

## Mudanças
- Nova folha CSS carregada após R9: `wwwroot/css/callmed-clinic-r9-controls.css`.
- Links que simulam botões, ações dos dashboards, atalhos e links de páginas internas recebem estados hover/foco/contraste consistentes. Destinos MVC e handlers permanecem iguais.
- Menu do perfil com dados de identificação, segurança, preferências e acessibilidade, instalação e sair; ações preservadas.
- Popover de acessibilidade com descrições, opções, leitura de tela, parar leitura e restaurar ajustes rápidos locais.
- Página MinhaConta estilizada: identidade do usuário, abas, cards, campos e escolhas acessíveis; POSTs e validações inalterados.
- Cache PWA alterado para `callmed-static-v5415-clinic-r91`; indicador de versão `v54.1.5-clinic-r9.1-controls`.

## Validação / limitações
- Validar build da solução com SDK .NET 10 antes de publicar.
- Inspecionar no ambiente de staging os 4 perfis, popovers abertos com clique/teclado, alternância de tema, tela MinhaConta em 390px e 1440px.
- Os ajustes rápidos de acessibilidade são locais ao navegador. Na página Minha Conta as preferências salvas na conta continuam controladas por formulário e botão Salvar.
- Não foram alterados controllers, banco, envio de mensagens, IA e lógica clínica.
- O patch é destinado à R9, não à V54.1.5 original nem às R7/R8.
