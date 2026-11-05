# Validação de perfil e encerramento — 05/11/2026

- Compilação Debug: sucesso.
- Publicação Release (.NET 8): sucesso.
- Servidor: 439 verificações passaram, incluindo regressões de canais, agendamento, acessos, avaliações, latência e redefinição de senha.
- Navegador: 18 verificações do perfil e 11 do widget passaram no Chromium.
- Perfil em celular: três abas, temas claro/escuro, sem rolagem horizontal.
- Fluxos HTTP reais de login, dados, foto, preferências, troca de senha, revogação e vínculo à própria conta, com EF InMemory e Identity.
- APIs de mensagens e respostas do modelo simuladas. Nenhuma mensagem real foi enviada.
- Permanece apenas o aviso preexistente CS8321 (CanalIcon não utilizada na Central).
- PostgreSQL/Render e aparelhos reais não foram exercitados nesta validação local.

O guia de publicação está em `docs/PERFIL-E-ENCERRAMENTO.md`. As capturas usam contas fictícias do teste HTTP.
