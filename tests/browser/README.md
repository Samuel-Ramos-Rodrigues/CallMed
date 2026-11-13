# Testes da CallMed no navegador

Use Node e .NET 10. Na raiz do projeto, compile `dotnet build tests/CallMed.Checks/CallMed.Checks.csproj`. Depois execute `npm install --prefix tests/browser`, `npx --prefix tests/browser playwright install chromium` e `npm test --prefix tests/browser`.

O teste inicia uma aplicação local com dados fictícios, verifica o fluxo de criptografia e permissões e encerra o processo ao terminar. Não utiliza Render, banco de produção, mensagens reais ou credenciais da clínica. As cópias protegidas de chaves fictícias e as capturas de validação ficam na pasta temporária do sistema.

Para usar um Chromium já instalado, defina `CALLMED_CHROME_BIN`. Para outro host .NET, defina `CALLMED_DOTNET_HOST`. `CALLMED_BROWSER_OUTPUT` permite escolher o diretório dos resultados. Os dados são fictícios, mas não reutilize as chaves ou senhas de teste em produção.

Para usar o build Release, defina `CALLMED_CHECKS_CONFIGURATION=Release`. `CALLMED_AXE_PATH` pode apontar para o arquivo local `axe.min.js` quando ele não está instalado no caminho padrão do Node.

Execute `node tests/browser/check-avatar-consultas.cjs` para testar fotos da conta e a separação de Solicitações/Consultas. A fixture `--perfil-consultas-fixture` inclui pedidos e consultas adicionais de teste; o script usa o servidor HTTP de loopback diretamente para exercitar o formulário nativo de upload. Não é uma configuração de produção.

Execute `node tests/browser/check-design.cjs` para a revisão de 47 páginas nos temas claro/escuro e com texto ampliado. Os scripts adicionais de Central, salas e acessibilidade continuam disponíveis nesta pasta.
