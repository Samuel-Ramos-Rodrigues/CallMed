# CallMed R9.3.8 — Acessibilidade: modo simplificado e alto contraste

## Evidências recebidas
- Home do Paciente em modo simplificado, tema escuro: os cinco atalhos tinham fundo quase branco e texto quase branco, tornando os nomes ilegíveis.
- Painel de acessibilidade: ao ativar **Alto contraste**, o indicador mudava para “Ativado”, mas a aparência principal permanecia praticamente igual.
- Reproduzidos localmente com CSS original da R9.3.7: atalho no tema escuro `rgb(244,248,252)` com texto `rgb(233,246,250)`; fundo principal com e sem alto contraste `rgb(17,29,45)`.

## Causa raiz
As opções em `wwwroot/js/core/accessibility.js` aplicam classes na raiz corretamente. Porém, o arquivo CSS consolidado contém regras posteriores com cores hexadecimais fixas e propriedades CSS redefinidas no `body.cm9-shell`. A classe `callmed-high-contrast` redefinia principalmente variáveis da raiz, sem conseguir sobrescrever esses valores no corpo e nos componentes. No modo simplificado, a regra antiga para `.cm21-simple-actions` usava `var(--text)` herdado do modo escuro, mas seu fundo vinha do `--surface-2` fixado com tom de tema claro pelo `body.cm9-shell`.

## Arquivos modificados
- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`: regras finais, restritas às classes de acessibilidade, para superfícies, atalhos, bordas e cores de texto em temas claro/escuro; suporte a ambos os modos ativados simultaneamente; correção de campos e ações principais em alto contraste.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml`: versão da interface `r9.3.8-accessibility`.
- `src/CallMed.Web/wwwroot/service-worker.js`: nome de cache atualizado para `r938-accessibility`.
- `tests/check-r938-accessibility-browser.py`: novo teste visual com Chromium, estados de ambos os modos e amostras de cores calculadas.
- Contratos dos testes legados de cache atualizados para o novo identificador.

## Testes locais executados
- Temas **claro e escuro** em larguras **320, 390, 820, 1024 e 1440 px** (10 cenários).
- Estados **normal**, **simplificado**, **alto contraste**, **simplificado + alto contraste**.
- Alteração efetiva de cor de fundo quando alto contraste é ativado, não apenas mudança no texto do botão.
- Contraste de pelo menos 4,5:1 nas amostras de texto calculadas para atalhos, cabeçalho interno e popover; sem rolagem horizontal nos componentes da fixture.
- Verificações estáticas existentes de arquitetura, CSS, cache PWA, fluxos e permissões: aprovadas.

## O que não foi testado nesta entrega
- **A versão nova não foi publicada** no Render; o comportamento de produção depende de deploy da R9.3.8.
- O teste visual foi feito em uma fixture local representativa, com CSS/JavaScript reais, **não** em todas as páginas reais autenticadas em todos os estados.
- Compilação .NET 10 e navegação autenticada da versão nova em produção não executadas nesta entrega.
- Nenhuma alteração de dados, API, integrações, banco ou controle de acesso foi realizada.

## Roteiro recomendado depois do deploy
1. No Paciente, abra Acessibilidade e ative Modo simplificado em tema escuro. Verifique legibilidade dos cinco atalhos.
2. Mantenha Modo simplificado ativo e ative Alto contraste; verifique fundo preto, texto branco, bordas definidas e foco do teclado.
3. Desative Modo simplificado e confira se a Home tradicional volta imediatamente.
4. Alternar Claro/Escuro ainda com alto contraste ativo; verificar texto/botões em ambos.
5. Nos perfis Médico, Atendente e Administrador, percorrer painéis, tabelas, Minha Conta, menus de ações e formulários com Alto contraste ativado.
6. Atualizar a página e confirmar a persistência das opções. Restaurar configurações de acessibilidade ao final.
