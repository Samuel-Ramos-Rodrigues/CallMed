# CallMed R9.3.7 — Diálogos e alertas compartilhados nos quatro perfis

**Base:** R9.3.6 (projeto completo independente). **Data:** 23/11/2026. Não publicado no Render.

## Escopo

Ampliar o ajuste da confirmação «Limpar mensagens da tela» para os diálogos e avisos de **Paciente, Médico, Atendente e Administrador**. As regras alteradas são globais e preservam os métodos de envio, cancelamento, consulta, prontuário, permissões e integrações.

### Mudanças reais no código

- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css`:
  - Em confirmações **destrutivas**, o botão principal agora tem estilo vermelho distinto dos botões comuns, nos temas claro e escuro. Isso abrange «Cancelar consulta», «Remover acesso», «Sair da lista de espera», «Encerrar exceção» e outras ações marcadas com `data-cm-confirm-tone="danger"` ou texto destrutivo.
  - O botão **X dos avisos** `.cm-feedback-banner-close` passou de 27×27 para pelo menos 44×44 px, com margem reservada para não encobrir o texto, foco visível e cores do tema.
  - Os botões **X de “Mais funções”** e **“Situação dos canais”** passaram a medir pelo menos 44×44 px, com foco visível. O diálogo de canais recebeu limite de viewport no celular.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml` e `src/CallMed.Web/wwwroot/service-worker.js`: versão de CSS/cache incrementada para `r9.3.7-shared-ui`.
- `tests/check-r937-cross-profile-browser.py`: 12 cenários do Chromium com estilos e `feedback.js` reais; valida contraste, largura, cancelamento seguro, confirmação única, Esc, funções e canais.
- Contratos antigos de versão de PWA e script de visual clássico ajustados para o cache atual.

### Comportamentos mantidos

- A ação «Limpar mensagens da tela» continua exigindo confirmação explícita e não exclui histórico persistido no servidor.
- `Esc` e cancelamento não executam operação.
- Nenhuma rota, autorização, regra do banco, envio de mensagem ou conteúdo clínico foi modificado.
- O assistente flutuante continua abrindo no canto inferior direito, conforme a R9.3.5.

## Verificações locais

- 12 combinações de quatro perfis simulados (`nova-patient`, `nova-doctor`, `nova-staff`, `nova-admin`) e tema/viewport: desktop 1440 escuro e claro, celular 390 escuro; testam CSS/JS **reais dos componentes**, não sessão autenticada ASP.NET.
- Contraste mínimo calculado de avisos nos cenários: 10:1; do botão destrutivo: 5,70:1.
- Formulários de cancelamento não são submetidos antes da confirmação; cancelar/tecla Esc interrompem; confirmar submete uma única vez no cenário simulado.
- Diálogos «Mais funções» e «Situação dos canais» não vazam da viewport e os botões de fechar medem pelo menos 44×44 px.
- A correção R9.3.6 do modal comum continua passando nos cenários 320, 390, 820, 1024 e 1440 px.
- Testes estáticos de CSS, MVC e permissões aprovados.

## Limites

O site em produção estava na **R9.3.3** durante as verificações. Os defeitos observados lá são da versão anterior e não constituem prova de falha na R9.3.7. Sem SDK .NET neste ambiente: build da solução não foi executado. Não houve teste destrutivo, transmissão de mensagens ou vídeo em produção.
