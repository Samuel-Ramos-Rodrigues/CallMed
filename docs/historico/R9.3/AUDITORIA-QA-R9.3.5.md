# CallMed — Auditoria complementar e correções R9.3.5

**Data:** 23/11/2026 (UTC). **Ambiente publicado analisado:** `https://callmed.onrender.com`, versão **R9.3.3** (commit `cdcbd5b4e2ad3f78cbdcdc10f3f46cbb8a2dedaa`). **Base da correção:** projeto ZIP independente **R9.3.4**. **Resultado do código:** R9.3.5, **ainda não publicado**.

## 1. Resumo executivo

- Foi possível concluir a visita autenticada que faltava do **Administrador**: `/FuncionarioPainel`, `/Organizacao`, `/Acessos`, `/Integracoes`, `/Auditoria`, `/Configuracoes`, `/Relatorios` e `/MinhaConta`.
- Foram repetidos testes de **permissões**. Paciente recebeu acesso negado em cinco rotas administrativas; Médico recebeu acesso negado em `/Acessos` e `/Auditoria` (outras tentativas ficaram incompletas); Atendente teve **acesso negado** em `/Acessos`, `/Integracoes` e `/Auditoria`, confirmado em teste direcionado com URL final. Um relatório automatizado anterior indicara incorretamente acesso permitido ao Atendente; **não se confirmou vulnerabilidade**. O código também exige `Admin` nessas rotas.
- O **chat central com fundo escurecido** persiste na **R9.3.3 publicada**. Sua mudança para janela ancorada no canto direito já havia sido preparada na R9.3.4 e foi conservada e refinada na R9.3.5.
- Os testes computados em Chromium encontraram **seis textos de contraste insuficiente no tema claro** na base R9.3.4, com razões aproximadas de **2,76:1 a 4,29:1**; foram corrigidos e passaram a **≥4,5:1 nos elementos examinados**. Os componentes medidos em tema escuro já passavam nessa amostra.
- A IA estava com **505 px de altura mesmo com apenas uma mensagem**; agora tem **420 px** em telas largas ou **410 px** em telas estreitas, expandindo para **540/490 px** quando há mais de três mensagens. Controles importantes passaram a pelo menos **44×44 px**.
- O fluxo do **agendamento em cinco etapas** passou com respostas simuladas das APIs, sem registrar consultas reais.
- **Não há homologação integral do CallMed em produção:** R9.3.5 necessita compilação .NET, implantação em ambiente de testes e nova inspeção autenticada, inclusive fluxos que escrevem dados.

## 2. BUGs e correções

### BUG-001 | Alta | Visual / Interação — Assistente central bloqueante
**Perfil:** Médico, Atendente, demais perfis com componente compartilhado.  
**URL:** `/MedicoPainel` e `/MinhaConta`; outras páginas usam o mesmo componente.  
**Reprodução em produção:** clicar na bolha da IA na R9.3.3.  
**Atual em produção:** modal central com backdrop e conteúdo por trás bloqueado.  
**Esperado:** janela compacta acima da bolha, fixa no canto inferior direito, sem backdrop, preservando largura da página, X e Esc funcionais.  
**Correção no ZIP:** alteração anterior da R9.3.4 preservada; na R9.3.5, dimensão inicial reduzida, altura adaptativa à quantidade de mensagens e botões de toque maiores. **Teste local aprovado** em desktop, tablet e celular; **não publicado**.

### BUG-002 | Alta | Contraste — textos secundários no tema claro
**Perfil:** paciente e componentes compartilhados.  
**URL/componentes:** `/Consulta/Create` (sobrelinha); `/` (jornada e ações rápidas); listas de atividades e histórico recentes.  
**Largura + tema:** Chromium, 1440 px, claro.  
**Reprodução:** medir cor de texto em relação à superfície efetiva em seis seletores, obtendo relações de 2,76:1; 3,42:1; 3,59:1; 3,74:1; 3,89:1; 4,29:1.  
**Esperado:** pelo menos 4,5:1 para textos comuns.  
**Correção no ZIP:** cores mais escuras e opacidade normalizada **apenas no tema claro** para preservar o escuro. **Reteste local:** nenhum elemento da amostra abaixo de 4,5:1.

### BUG-003 | Média | UX / Toque — chat grande e botões de alvo pequeno
**Perfil:** todos com assistente.  
**Componente:** `_AssistenteFlutuante.cshtml`, CSS do widget.  
**Atual na base R9.3.4:** janela de 505 px mesmo com uma saudação; fechar/limpar com 38 px e enviar com 43 px.  
**Esperado:** expansão discreta e controles confortáveis em mobile.  
**Correção no ZIP:** modo `compact` / `expanded` controlado pela contagem de mensagens; altura e largura reduzidas, controles ≥44 px; o layout **não é modal**.

### BUG-004 | Média | Navegação redundante — Minhas teleconsultas
**Perfil:** paciente, `/Jornada?modo=online`.  
**Evidência:** captura enviada pelo usuário da R9.3.3.  
**Atual:** barras com ações duplicadas e link para a própria página.  
**Correção:** preservada a limpeza de atalhos da R9.3.4 na R9.3.5; não é correção nova desta rodada. Necessário retestar após publicar.

### BUG-005 | Alta | Contraste — destaque da jornada no modo escuro
**Perfil:** paciente, início (`/`).  
**Evidência:** captura enviada pelo usuário da R9.3.3: etapa destacada com fundo claro e título apagado.  
**Correção:** R9.3.4 já substitui por superfície escura e texto legível; mantido na R9.3.5. O teste local da etapa ativa mediu razão aproximada de **8,78:1**. Necessário conferir no site depois do deploy.

### BUG-006 | Média | Espaçamento — títulos próximos às bordas
**Perfil:** paciente, início (`/`) e `/Consulta/Create`.  
**Evidência:** imagens enviadas pelo usuário da R9.3.3.  
**Correção:** paddings de painéis/guia/formulário da R9.3.4 preservados; teste local confirmou **recuo ≥18 px** nos painéis principais da amostra e ausência de sobreposição guia/formulário.

## 3. Cobertura de páginas e perfis

`V` = rota visitada em produção, mas **não** significa que todas as ações foram executadas. `L` = componente relacionado validado só em Chromium local. `—` = não executado ou não se aplica.

| Área | Público | Paciente | Atendente | Médico | Admin |
|---|---|---|---|---|---|
| Login / entrada pública | V | V | V | V | V |
| Home pública, privacidade, termos, app, páginas offline, health | V | — | — | — | — |
| Painel principal autenticado | — | V | V | V | V |
| Agendamento em 5 etapas | — | V + L (API simulada) | — | — | — |
| Consultas, jornada, vídeo (navegação sem entrar na sala) | — | V | V | V | — |
| Fila de espera | — | V | V | — | — |
| Confirmações | — | V | V | — | — |
| Exames / histórico | — | V | — | — | — |
| Central, solicitações, recepção | — | — | V | — | — |
| Agenda, disponibilidade, exceções | — | negado na matriz de acesso | V | agenda geral negada no teste anterior | — |
| Operação / comparação do piloto | — | — | V | — | — |
| Organização / menu de funções | — | — | — | V | V |
| Acessos, integrações, auditoria | — | negados | negados (reteste) | Acessos/Auditoria negados | V |
| Configurações da clínica | — | — | negada | — | V |
| Relatórios | — | — | — | — | V |
| Minha Conta / preferências | — | visitas anteriores | V | V | V |
| IA aberta e fechada | — | teste anterior + L | V + L | V + L | V + L |

## 4. Testes adicionais realmente executados

### No site publicado (R9.3.3)
- **Administrador:** login e oito rotas internas abertas; leitura de gráficos/controles; menu de funções e tecla Esc; Tab usado nos controles disponíveis.
- **Permissões:** atendente com acesso negado em `/Acessos`, `/Integracoes`, `/Auditoria`; paciente negado em `/Acessos`, `/Auditoria`, `/Agenda`, `/FuncionarioPainel`, `/Integracoes`; médico negado em `/Acessos` e `/Auditoria`. Outros destinos podem continuar pendentes.
- **Proteção de dados:** nenhum cadastro, consulta, mensagem real, confirmação, cancelamento ou alteração de prontuário foi realizado.

### No projeto R9.3.5 (Chromium local, APIs falsas)
- **Resoluções:** 1440 px, 1024 px, 820 px, 390 px; também 320 px e 720 px como verificações adicionais. Em componentes de teste, **sem overflow horizontal**.
- **Temas:** modo claro e escuro, alternância real via botão e sincronização de `aria-label`, sem erro do JavaScript.
- **WCAG parcial:** contraste calculado em componentes reais da amostra; botão fechar/limpar/enviar da IA com alvos de ao menos 44×44 px; foco devolvido à bolha depois de Esc; `Ctrl+K` abriu a busca e Esc a fechou.
- **IA:** sem backdrop, sem alteração de largura do conteúdo; janela ancorada no canto, tanto compacta (uma saudação) quanto expandida (seis mensagens); fechamento pelo X e Esc.
- **Agendamento:** estados da etapa 1 à 5, especialista → profissional → data → horário → confirmação, validação de botão desabilitado, seleção de modalidade online, esvaziamento das escolhas ao trocar de especialidade, estado sem profissionais.
- **Análise estática:** 113 páginas Razor, 51 arquivos de controller identificados, 517 referências MVC verificadas, zero referências inválidas; verificações de arquitetura, CSS, fluxos e permissões de controller aprovadas.

## 5. O que ainda não foi testado integralmente

1. **Compilação e suíte .NET 10:** SDK `dotnet` não está instalado no ambiente de trabalho atual. Nenhum resultado de `dotnet build` ou teste de integração foi inventado.
2. **R9.3.5 em produção:** não houve deploy deste ZIP. O site analisado permanece com R9.3.3 na data acima; testes autenticados não homologam a versão nova.
3. **Mobile real e zoom 200%:** os tamanhos foram simulados em Chromium local usando componentes representativos. Não há confirmação de todas as 113 páginas em telefones reais, nem auditoria WCAG completa de zoom 200%/leitor de tela.
4. **Fluxos que escrevem dados:** cadastros/edições, envio WhatsApp/SMS/e-mail, confirmações, reagendamento, cancelamento, check-in, vídeo e dados clínicos não foram testados ponta a ponta na produção por segurança. Exigem ambiente de homologação com registros `QA-`.
5. **URLs e tema de toda página:** cada rota visitada não foi validada individualmente nas quatro larguras e em ambos os temas. O resultado não autoriza declarar o projeto inteiro livre de erros.
6. **Possíveis problemas não confirmados:** formato de data dos controles nativos, paginação de 21 consultas, rolagem horizontal da tabela de espera, ausência de IA em páginas públicas e rodapés offline diferenciados. Não foram convertidos em bugs sem prova de falha.

## 6. Mudanças de arquivos (R9.3.4 → R9.3.5)

- `src/CallMed.Web/wwwroot/css/callmed-clinic-r93.css` — contraste do tema claro e dimensionamento/controles da IA.
- `src/CallMed.Web/wwwroot/js/atendimento/assistente-flutuante.js` — densidade `compact`/`expanded` a partir do histórico real de mensagens.
- `src/CallMed.Web/wwwroot/service-worker.js` — nova chave de cache de assets.
- `src/CallMed.Web/Views/Shared/_DocumentHead.cshtml` — metadado de versão atualizado.
- `tests/check-r935-responsive-browser.py`, `tests/check-r935-booking-browser.py`, `tests/check-r935-chat-history-browser.py`, `tests/check-r935-permissions.cjs` — novos testes de regressão.
- Contratos estáticos atualizados para o novo versionamento PWA.

**Próximo requisito de homologação:** publicar R9.3.5 primeiro em **ambiente de teste**, executar `dotnet restore/build/test`, repetir as capturas de Paciente/Atendente/Médico/Admin em claro/escuro e só depois decidir sobre deploy em produção.
