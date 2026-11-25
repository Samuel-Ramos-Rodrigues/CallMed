# CallMed R9.3 — reorganização visual sobre R9.1

## O que mudou

- Removido o bloco global `_R8Context`: havia 3 níveis de navegação em páginas internas. Breadcrumb e menu continuam; ações específicas de cada página foram mantidas.
- Barras de orientação R3/R4/R5 existentes são apresentadas como barra compacta de **ações**, ocultando títulos/descrições redundantes (links preservados). Em Minha Conta e Salas, a barra duplicada da view foi removida por haver ações equivalentes no cabeçalho da própria página.
- CSS ativo consolidado em **`wwwroot/css/callmed-clinic-r93.css`** (R7 + R8 + R9 + R9.1 + overlays na ordem original), com 33 declarações idênticas redundantes eliminadas e 11 regras da barra R8 desativada retiradas. Isso reduz a cascata entre arquivos; não é uma reescrita integral de cada componente legado.
- Cabeçalho/mobile: uma navegação de desktop até 740 px e a barra inferior no mobile, evitando dupla navegação.
- Popovers de perfil, Aa e tema limitados pela altura do dispositivo. IA flutuante abaixo do header no desktop, e tela inteira em mobile, incluindo semântica modal/Tab/foco e viewport do teclado.
- CSS de versões inativas removido do pacote. **Cinco folhas `nova-*` continuam** porque as páginas públicas `offline.html` e `contingencia.html` as referenciam.
- Tema escuro preservado e revisado em componentes com cores claras forçadas pela R9.1 (perfil, acessibilidade, conta, campos, atalhos, IA), incluindo alto contraste. A escolha Claro/Escuro/Sistema continua persistida pelo JavaScript original; cor da barra do navegador harmonizada com o novo fundo escuro.
- Cache do PWA atualizado; classes e lógica original da Central de Atendimento não foram alteradas; canais Gmail/Brevo, WhatsApp e SMS, banco e backend permanecem intocados.

## Antes de publicar

1. Descompacte o projeto completo ou aplique o patch na R9.1 mais recente. Não misture com R9.2/Gmail de base divergente sem comparar o diff.
2. Compile `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release` com SDK .NET 10 adequado (não presente no ambiente de edição).
3. Teste em ambiente de homologação com paciente, médico, atendente e admin, em 390 px, 768 px e 1440 px, tanto no tema claro como no escuro.
4. Verifique IA aberta/fechada, Aa, perfil, tema, PWA atualizado, Teleconsulta, Agenda, Jornada, Lista de espera, Minha Conta, Cadastros e Central de Atendimento.
5. Só publique se o comparativo do layout e as operações existentes estiverem corretos.

## Notas

- A regra de `app-header` na R9.3 fica abaixo de menus `dialog` e acima da IA no desktop; no celular a IA aberta tem camada superior.
- Nenhuma alteração em controllers, bancos, migrações, credenciais ou endpoints.
- Esta revisão **não foi compilada com dotnet** nem validada em todos os perfis autenticados *após* o patch; não afirma homologação em produção.
