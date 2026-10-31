# CallMed — Central de Atendimento Origem

Data: 31/10/2026

Esta entrega aplica a direção visual aprovada da Central de Atendimento:

- Logo horizontal original `wwwroot/images/logo-callmed-horizontal.png`, usada no cabeçalho da Central.
- Navegação superior e caixa de entrada integrada à área principal, sem terceira coluna fixa.
- Ícones de origem para E-mail, WhatsApp, SMS, Site, Telefone e Presencial na lista e nas mensagens.
- Ícones renderizados também para mensagens novas carregadas pelo polling da Central.
- Busca de texto instantânea apenas nas conversas carregadas; filtros de canal e modo continuam funcionando via GET.
- Resumo de métricas recolhível e detalhes de paciente/conversa em painel expansível.
- CSS renovado com suporte aos temas claro e escuro, foco, dispositivos móveis e redução de movimento.

## Limites

O canal é armazenado na entidade da conversa; portanto, todas as mensagens de uma conversa usam o canal dessa conversa. Esta entrega não altera o esquema do banco de dados nem adiciona uma coluna de canal por mensagem.

Ações, controllers, processamento semântico da IA, Gmail API, Brevo SMTP, SMS e regras de aprovação de e-mails foram preservados.

## Validações locais

- `node --check` em `wwwroot/js/atendimento.js`: OK.
- Análise sintática com tinycss2 em `wwwroot/css/callmed-atlas.css`: OK.
- Chromium/Playwright: layouts em 390, 768 e 1440 px, busca e resumo recolhível: OK.
- Integridade do ZIP: OK.
- Compilação ASP.NET Core 8: não executada (SDK .NET não instalado no ambiente).
- Integração real / Render: não testados.

## Testar após deploy

1. Publicar em ambiente de homologação e confirmar a compilação do Dockerfile.
2. Abrir a Central e alternar entre conversas de E-mail, WhatsApp, SMS e Site.
3. Checar ícones nas mensagens antigas e novas sem atualizar a página.
4. Aplicar filtros, buscar na lista, abrir detalhes e alterar o modo de atendimento.
5. Testar um rascunho de IA por e-mail e confirmar aprovação antes do envio.
6. Verificar também o tema escuro e o celular.
