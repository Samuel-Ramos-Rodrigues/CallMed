# CallMed Revolution R8 — alterações reais

Base: **CallMed V54.1.5 Studio R7**. Esta versão altera arquivos Razor e CSS, não somente prévias conceituais.

## O que foi alterado

- `Views/Home/Index.cshtml`: painel do paciente reconstruído com compromisso principal em área escura, data grande, ações prioritárias e linha horizontal de cuidado; dados continuam vinculados ao `HomeViewModel`.
- `Views/MedicoPainel/Index.cshtml`: agenda em linha do tempo, resumo do plantão e próximo atendimento em painel clínico; vagas e bloqueios preservados com formulários originais.
- `Views/FuncionarioPainel/Index.cshtml`: administrador com indicadores tipográficos, gráfico circular calculado da agenda e prioridades; atendente com painel em três filas de trabalho.
- `Views/Atendimento/Index.cshtml`: nova identificação visual da Central, mantendo a estrutura de conversa e os controles originais.
- `_Layout.cshtml` e `_R8Context.cshtml`: uma barra contextual compacta substitui duas anteriores; logo, menu, navegação mobile e bolha real da IA permanecem.
- `css/callmed-revolution-r8.css`: identidade visual própria, com regras específicas para dashboards, formulários, tabelas e Central, além de breakpoints mobile. O CSS consolidado R7 foi mantido como compatibilidade para componentes complexos já existentes (agenda, atendimento, vídeo, login e formulários).
- `_DocumentHead.cshtml` e `service-worker.js`: versão e cache atualizados; páginas autenticadas continuam fora do cache offline.

## O que não foi alterado

Controllers, serviços, banco de dados, EF migrations, canais, envio de mensagens, regras de agendamento, WebRTC/teleconsulta, API Gmail, Evolution API, Brevo, SMS, APK Android.

## Validação feita

- CSS R8 analisado pelo PostCSS sem erro de sintaxe.
- JavaScript do Service Worker verificado com `node --check`.
- 21 contratos de fluxo estáticos passaram em `scripts/check-flows-v54-1-4.py`.
- `tests/ux-contract.cjs` passou.
- Referências `asp-controller` das novas views conferidas contra classes de controller existentes.
- 4 telas de exemplo renderizadas com o CSS R7 + R8 em 1440 e 390 pixels, sem rolagem horizontal do documento.
- Os exemplos usam dados ilustrativos, NÃO são capturas de telas Razor autenticadas.

## Validações ainda necessárias antes de produção

1. Instalar SDK .NET exigido pelo projeto e executar `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release`.
2. Publicar em staging, usar os quatro perfis e testar rotas de agenda, agendamento e check-in, médico e teleconsulta, Central e IA, páginas de gestão e acessibilidade.
3. Verificar que o marcador `document.querySelector('meta[name="callmed-ui-version"]')?.content` retorna `v54.1.5-revolution-r8`.
4. Conferir feedback/estados vazios e formato mobile em aparelhos reais.
5. Se necessário, fazer rollback reimplantando o projeto completo da R7.

**Nota de transparência:** a reformulação estrutural direta ocorreu nos painéis dos quatro perfis; as 110 views internas não foram reescritas individualmente, embora recebam a aparência compartilhada R8. A validação de execução .NET e testes autenticados ainda não ocorreu.
