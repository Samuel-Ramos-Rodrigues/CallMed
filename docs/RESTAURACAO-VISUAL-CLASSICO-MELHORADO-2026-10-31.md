# CallMed Clássico Melhorado — restauração visual de 31/10/2026

## Base
- Projeto funcional: CallMed-Origem-Central-Canais-Integrados.zip (IA semântica, Gmail, Brevo e atualizações do atendimento preservados).
- Estilo: restaurado de CallMed-Gmail-Brevo-IA-email-devolver-corrigido.zip, antes do CSS Aurora/Atlas/Lumen/Origem.
- Layout da Central e do shell restaurados ao design original de três colunas e navegação no topo.

## Melhorias discretas
- Melhor legibilidade no chat, espaçamentos e contraste em modo escuro.
- Busca rápida na caixa de entrada, limitada às conversas carregadas na tela.
- Ícones dos canais E-mail/WhatsApp/SMS/Site/Telefone/Presencial em conversas e mensagens, incluindo as recebidas via polling JavaScript.
- Acesso direto à Central no menu horizontal.
- Nenhum novo CSS de redesign carregado; ajustes concentrados no final de callmed.css.

## Não alterado
- Serviços C#, banco de dados, rotas, modelos e configurações de integração.
- Aprovação obrigatória de rascunhos de e-mail.

## Limitações de validação
- Compilação dotnet e testes externos Gmail/Gemini/Brevo dependem de SDK e credenciais de integração.
