# CallMed — retorno ao visual original da V35

**Base funcional preservada:** projeto completo V52 com a distribuição Android nativa Release `50.0.0`, os controllers, serviços, migrations, APIs, testes e novas funcionalidades. **A alteração é de interface.**

## Como foi restaurado

O arquivo original `CallMedCrud/wwwroot/css/callmed.css` foi lido diretamente do pacote `CallMed_V35_SENAI_Visual_Corrigido_Login_V34.zip`, sem copiar de versões intermediárias. O SHA-256 do conteúdo original é `459e9426ddcbd57aac1625e8f220de61583f359adc8fc6098a5e5818d7a7ab65`.

Como as views atuais do projeto utilizam nomes de classes e componentes que **não existiam** na V35 (`cm50-*`, calendário atualizado, fila inteligente, Android/download, confirmação, rascunhos de IA), o CSS original foi mantido no primeiro módulo e adicionamos quatro folhas de **adaptação** de visual. Dessa forma, o tema é V35, mas o código funcional continua atual.

- `nova-00-design.css`: CSS autoral original da V35 + correspondência de variáveis com componentes novos.
- `nova-10-shell.css`: cabeçalho, menus e painéis dos quatro perfis, com identidade V35.
- `nova-20-fluxos.css`: agendamento, calendários, Central de Atendimento, salas e jornada.
- `nova-30-acesso.css`: login, cadastro e página pública, corrigindo contraste e responsividade.
- `nova-40-modulos.css`: assistente flutuante, notificações, solicitações, avaliações, gestão e download do Android.

A arquitetura de cinco CSS do projeto foi preservada para evitar quebrar os testes e a aplicação atual, mas **o conteúdo V46–V52 foi substituído**. O V35 mantém azul/índigo/roxo, Manrope, cartões escuros e navegação superior sem barra lateral.

## Preferências e cache

O tema padrão para usuários sem preferência salva foi definido como **escuro**. Uma preferência antiga de tema claro salva no navegador continuará sendo respeitada; o tema pode ser ajustado em Aparência.

O service worker usa o identificador `callmed-static-v103-v35-restored` para expirar os caches visuais anteriores. Todos os CSS continuam com `asp-append-version="true"`.

## Validação realizada

- Inventário do projeto: 109 Razor views, 51 controllers, 393 referências MVC e 57 recursos estáticos (via scripts de auditoria).
- `node tests/css-integrity.cjs`: aprovado.
- `node tests/ux-contract.cjs`: aprovado.
- `python scripts/check-css.py`: aprovado.
- `python scripts/check-architecture.py`: aprovado.
- `python scripts/check-v35-classic-visual-contracts.py`: aprovado.
- Sintaxe das folhas verificada com `tinycss2`; JS do tema verificado com `node --check`.
- **28 prévias estáticas**: 7 experiências representativas × desktop (1440px) e celular (390px) × temas claro e escuro, sem overflow horizontal ou erros de JS. Foram usados dados fictícios; essas prévias não são testes autenticados reais.
- Conferência do APK: arquivo original do pacote funcional V52 preservado sem modificações.

## Fora do escopo / pendências

A estrutura Razor das telas V50 foi mantida e adaptada à V35; **não é uma restauração byte a byte das telas da V35**. Não houve alteração de contratos, controllers, regras clínicas ou banco de dados.

Não foi possível compilar o .NET nem testar todas as páginas com quatro sessões reais neste ambiente. Antes de publicar, realizar `dotnet build CallMed.sln`, os testes automatizados e conferir visualmente os quatro perfis, especialmente atendimento, agendamento, teleconsulta, solicitações, cadastro e download do APK.

Não houve merge no GitHub nem deploy no Render.

**Instalação:** recomende-se fazer backup da versão atual, aplicar a versão completa ou sobrepor os arquivos contidos no pacote leve na raiz do repositório. Após deploy, limpar eventual cache de site/PWA e recarregar a página com zoom 100% (`Ctrl+0`).
