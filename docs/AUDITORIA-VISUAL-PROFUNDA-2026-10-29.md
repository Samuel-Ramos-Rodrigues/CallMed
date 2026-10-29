# Auditoria visual profunda — CallMed (29/10/2026)

## Escopo e método

Revisão estática do pacote completo usado como base, com inventário das folhas CSS, layouts Razor, lógica de tema e cache do service worker. Foram conferidos os layouts autenticado, público e de autenticação; os 14 arquivos CSS do projeto; as 80 views Razor encontradas em `Views` e `Areas/Identity/Pages`; o arquivo `wwwroot/js/theme.js`; e `wwwroot/service-worker.js`.

Esta é uma auditoria de código estática. Não foi possível afirmar validação visual de cada rota em navegador nem compilação .NET neste ambiente.

## Achados principais

1. **Cascata excessivamente fragmentada.** O layout autenticado carrega `site.css` e `acessos.css` via `_DocumentHead`, depois `modules.css`, `clinic-theme.css`, `clinic-redesign.css`, `clinic-final-fixes.css`, `clinic-polish-2026.css`, `clinic-unified-palette.css` e `clinic-functional-fixes.css`. O layout público e o de autenticação também carregam múltiplas camadas que se sobrepõem. Isso faz com que regras posteriores vençam regras anteriores por ordem/especificidade, dificultando saber qual declaração governa uma tela.
2. **Paleta anterior sem unidade.** Havia tons de verde em `clinic-unified-palette.css`, cores azul-petróleo em `clinic-theme.css` e declarações adicionais em `clinic-functional-fixes.css`, além de valores fixos espalhados por estilos legados.
3. **Estados de botão frágeis.** Algumas regras definem fundo, mas não asseguram a cor do texto para `hover`, `active`, `disabled`, links com aparência de botão e conteúdo interno. A opacidade herdada também pode deixar rótulos apagados.
4. **Temas não estavam alinhados em todos os lugares.** `theme.js` ainda definia `theme-color` de navegador para tons verdes. O CSS e a cor da barra do navegador podiam, portanto, divergir.
5. **Regras de estilo inline.** Foram localizados 8 atributos `style` em 6 views. Eles podem escapar à manutenção por tokens globais e devem ser migrados para classes em uma etapa específica.
6. **Escopo amplo.** Foram identificadas 80 views Razor, cobrindo páginas públicas, identidade/autenticação, agenda, pacientes, médicos, consultas, atendimento, relatórios, configurações, histórico de exames e outros módulos. A presença de uma regra global não substitui testes funcionais e visuais de cada rota.

## Alterações aplicadas nesta entrega

- Adicionado `wwwroot/css/visual-system-2026.css` como última folha nos layouts autenticado, público e de autenticação.
- Nova identidade: azul ardósia, índigo e violeta; modo claro em cinza-azulado (não branco puro); modo escuro em azul-marinho profundo (sem verde como cor principal).
- Tokens de fundo, superfície, texto, borda, marca, foco, estados informativos e sombras centralizados na nova folha.
- Estados explícitos de botões primários, secundários, perigosos, hover, foco, active e disabled, incluindo `-webkit-text-fill-color` para reduzir casos de texto apagado em componentes herdados.
- Campos, placeholders, cartões, tabelas, navegação e popovers recebem cores de tema consistentes.
- `theme.js` atualizado para refletir a nova cor de navegador: `#eef2fa` no modo claro e `#101827` no escuro.
- Cache do service worker avançado para `callmed-static-v40-visual-system-2026`.
- Todos os arquivos existentes no pacote-base foram preservados; somente o CSS novo e este relatório foram acrescentados.

## Verificações realizadas

- Contagem do pacote-base: 416 arquivos (entradas de arquivo, sem contar diretórios).
- Entrega: 418 arquivos; zero arquivos removidos; dois arquivos novos (`visual-system-2026.css` e este relatório).
- Os três layouts principais incluem a nova folha depois das folhas anteriores.
- Chaves `{}` do novo CSS balanceadas.
- Contraste calculado para pares representativos: texto branco em botão índigo `#4059d9` = 5,76:1; texto principal em superfície clara `#17233b` / `#f8faff` = 15,01:1; texto principal escuro `#f1f5ff` / superfície `#182338` = 14,40:1. São verificações de tokens, não de todos os componentes individualmente.

## Limitações e próximos passos recomendados

- Não foi executado `dotnet build`, nem teste de navegador automatizado, pois o ambiente não dispõe de SDK/runtime adequado para confirmar a compilação da aplicação.
- Não foi feita uma captura visual de todas as 80 views. Após instalar, testar ao menos: início público, login, cadastro, recuperação de senha, painel de funcionário, agenda, pacientes, médicos, consultas, atendimento, relatórios, configurações, histórico de exames e telas de formulário/erro.
- A camada nova normaliza visualmente os componentes comuns, mas não remove ainda cada declaração CSS antiga. Remover regras antigas em lote sem testes por rota poderia quebrar componentes específicos. A limpeza destrutiva deve ser feita por módulo, com comparação visual e teste funcional.
- E-mails HTML têm estilos inline próprios e não são cobertos por esta camada; devem ser revisados separadamente caso também seja necessário mudar a paleta dos e-mails.
