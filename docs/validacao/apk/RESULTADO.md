# Validação do download Android — 05/11/2026

- APK fornecido pelo usuário, pacote com.callmed.app, versão 22.2.0 e assinatura de produção conferidos.
- Assinatura criptográfica APK v2 válida, verificada pelo Android apksig 8.13.2.
- Compilação Debug e publicação Release concluídas.
- APK e release.json presentes na publicação, idênticos aos arquivos de origem.
- 452 verificações do servidor passaram, incluindo 13 do download e todas as regressões anteriores.
- 12 verificações de interface passaram no Chromium, com HTML real do MVC e CSS/JS do projeto.
- Testes do navegador cobrem download efetivo, hash, instruções, indisponibilidade e temas claro/escuro em 320, 390 e 1280 pixels.
- A regra do Git permite apenas o APK público e continua ignorando as chaves privadas do TWA.
- Nenhuma mensagem real foi enviada. Não houve instalação no aparelho nem publicação no Render durante esta validação.
- Permanece apenas o aviso preexistente CS8321 (CanalIcon não utilizada na Central).
