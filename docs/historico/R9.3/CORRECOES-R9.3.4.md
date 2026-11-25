# Correções CallMed R9.3.4 — Chat no canto, atalhos e espaçamento

Base: R9.3.3 independente. Nenhuma alteração em integrações, banco ou fluxo de pagamento.

- IA: não modal, abre no canto inferior direito, sem backdrop e sem empurrar o formulário. Fechamento pelo X ou Esc; foco livre para continuar a página.
- Teleconsultas do paciente: removida a barra de atalhos duplicados (incluía um botão para a própria página); mantido apenas “Todas as consultas” na visualização online e “Histórico e remarcações” na visualização geral. Menu principal já oferece “Agendar” e “Teleconsulta”.
- Agendamento do paciente: removida barra de ações duplicadas. Mantidos acessos via menu e guia de espera. Corrigido espaçamento entre guia e formulário e padding do cabeçalho.
- Início do paciente: adicionados recuo e bordas aos cards “Sua jornada de cuidado” e “O que deseja fazer?”; corrigidos fundo e contraste do passo ativo no tema escuro e legendas.
- Service worker: cache invalidado após atualização dos estilos.

## Revisão obrigatória após publicação

1. Entrar como paciente, abrir /Jornada?modo=online: conferir uma ação contextual apenas e nenhuma barra vazia.
2. Abrir /Consulta/Create: verificar espaçamento do guia e formulário, opções de modalidade e cinco indicadores.
3. Abrir IA: confirmar que surge acima da bolha no canto inferior direito, com fundo da página sem escurecimento; fechar pelo X e Esc.
4. Testar / em modo escuro: blocos inferiores com padding e etapa 2 legível.
5. Testar 390px, 820px, 1440px e acessibilidade com texto maior.

Verificações estáticas e teste do Chromium local não substituem teste funcional autenticado após deploy.
