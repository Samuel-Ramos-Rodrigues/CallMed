# CallMed V34 — aplicação das auditorias

Base: `CallMed-V33-Perfil-Consultas-Projeto-Completo.zip`, SHA-256 `52a0440623298229b65aeb7182057dc5b9a27e5044272bca9193ddadfb6d9cd5`.

A versão reúne correções dos 18 achados técnicos e dos pontos de interface revisados. Mantém a logo horizontal, a paleta clássica, a navegação superior e a Central em três colunas nas telas largas. As integrações existentes e o projeto TWA foram preservados.

## Segurança, funcionalidades e operação

| ID | Alteração aplicada | Verificação e condição de implantação |
|---|---|---|
| A01 | Cadastro público não assume nem reativa pacientes existentes. A recepção pode enviar convite após conferir identidade e e-mail; não há senha pronta. | Testes negativos de cadastro/inatividade/conferência; convite com e-mail capturado localmente. Envio real depende da integração configurada. |
| A02 | Fila de ligações com responsável, conclusão e ligação com a consulta. Tarefas entram nas pendências e possuem acesso em Solicitações. | Predicado de pendências e controllers compilados; página integra os componentes existentes. Concluir ligação não confirma consulta automaticamente. |
| A03 | Removida a cópia automática de papéis no boot. Gestão aceita conjuntos profissionais compatíveis e atribui o papel conforme a profissão. | Regressão de conta com Medico + Psicologo; perfis incompatíveis continuam recusados. |
| A04 | Links de recuperação/convite usam `PublicOrigin` HTTPS, sem depender do Host enviado pelo visitante. AllowedHosts restrito por padrão. | Validação da origem e teste HTTP de Host adulterado no runtime publicado. Domínio próprio deve ser configurado no Render. |
| A05 | Limite por IP e contadores compartilhados por identificador, com contenção de reenvio e resposta genérica. | Regressão e oito solicitações concorrentes em PostgreSQL; somente uma permitida na janela. |
| A06 | Refresh tokens giram atomicamente por sessão. Reutilização revoga a família; limite absoluto de 14 dias. | HTTP e PostgreSQL: token anterior recusado e sucessor revogado após reutilização. Sessões legadas podem exigir novo login. |
| A07 | Key ring protegido com certificado externo ao banco. Job protege chaves legadas mantendo IDs e material criptográfico. | Dump lógico sem segredo mestre em claro; restauração de link após novo provedor lógico. **Exige certificado/senha e backup externo.** |
| A08 | Atualizados pacotes e removidas referências de scaffolding/tooling desnecessárias ao runtime. | Grafo NuGet consultado sem pacotes vulneráveis sinalizados; CI inclui bloqueio de novos alertas. |
| A09 | Projeto, testes, SDK de referência e Docker atualizados para .NET 10. | Build Release e publish executados com o novo SDK. |
| A10 | Android preserva sessão em recusas pontuais 403. Credenciais inválidas/revogadas da API retornam 401. | Testes HTTP, transporte Android e Robolectric. APK recompilado, com a assinatura anterior. |
| A11 | Filtros, totais, ordenação e paginação de Consultas passam ao PostgreSQL. Pacientes também limita linhas e calcula totais no servidor. | Massa de 53 consultas: páginas de 20, sem repetição e filtro SQL. Benchmark de carga/p95 real não foi realizado. |
| A12 | `/health/live` e `/health` para processo; `/health/ready` para banco, com timeout e resposta sem credenciais. | Runtime publicado e queda simulada do PostgreSQL; configurar readiness no Render. |
| A13 | Job `--migrate`, lock PostgreSQL, baseline e etapas V12–V37 versionadas. Boot normal valida schema sem executar DDL. | Banco vazio, reaplicação, upgrade de schema legado de teste e preservação de cadastro. Restaurar backup real permanece uma tarefa de homologação. |
| A14 | Cabeçalhos globais, proteção de frame/base/objeto/form e CSP de scripts em Report-Only. Políticas mais específicas das rotas são preservadas. | HTTP no runtime publicado e regressão web. Enforcement completo de scripts exige migrar os scripts inline e revisar violações em homologação. |
| A15 | Contratos de navegação atualizados, auditor de partials corrigido e CI para .NET, PostgreSQL, dependências, navegador e Android nativo. | Suítes locais executadas; workflow entregue para executar no GitHub. Não houve push nem execução remota. |
| A16 | Uma representação Practitioner compartilhada na leitura e no bundle, respeitando CRM/UF, CRP e outros registros. | Testes de recursos gerados. Homologação com fornecedor PEP continua necessária. |
| A17 | Webhook WhatsApp persiste a entrada e responde sem aguardar a IA. Worker recupera pendências; lease de banco coordena instâncias. | Cancelamento, duplicata, retomada sem resposta duplicada e exclusão entre processos lógicos. Entrega incerta do provedor continua exigindo conferência. |
| A18 | Limpeza em lotes da sinalização com mais de 15 minutos, índice por data e descarte de contadores expirados. | Teste preserva sinal ativo e remove o antigo. Histórico de presença é mantido. |

## Telas e CSS

- Central: textos secundários ampliados e preservação da composição clássica.
- Agendamento: etapas exibidas conforme as escolhas, estados iniciais corretos, resumo antes da confirmação e espaço entre ações.
- Preparação: próximo passo destacado, etapas concluídas recolhidas, alinhamento da criação/restauração de chave corrigido e upload em português.
- Recepção: apresentação inicial compactada e rodapé duplicado removido.
- Pacientes: indicadores mais compactos, documentos/telefones formatados na listagem e paginação.
- Configurações: identificação como Informações do sistema, com orientação sobre preferências da conta.
- CSS: um bundle `callmed.css`, gerado de 13 módulos mantidos. Nenhuma folha adicional de sobrescrita foi introduzida. Os controles de foto da conta preservam sua interação própria.

## Implantação

**Leia [as instruções de implantação](IMPLANTACAO.md) antes do deploy.** Na implantação original da V34 foram introduzidos o certificado de Data Protection e o schema 37. A V35 mantém essa configuração; quem já a utiliza não precisa gerar outro certificado. O modelo atual é `RENDER.env.example`; as integrações existentes devem ser mantidas.

## Limites

A execução local não certifica LGPD, CFM/CFP/ANS ou acessibilidade. Não foram enviados e-mails, WhatsApp ou SMS reais, acessados dados da clínica nem implantadas alterações no Render. TURN em redes externas, instalação física do APK, validação com pacientes/profissionais, carga representativa e restauração do backup/chaves reais ainda precisam de homologação.

O relatório original da V33 permanece como histórico do diagnóstico. Esta entrega contém o código corrigido; seus resultados de validação estão em `validacao-v34/`.

## Validação executada da V34

| Conjunto | Resultado |
|---|---|
| Build e publish Release | Aprovados; build sem avisos/erros. |
| .NET e domínio | 13 execuções, 1011 asserções aprovadas. |
| PostgreSQL real isolado | 6 execuções, 51 asserções aprovadas. |
| Navegador | 8 suítes, 490 asserções aprovadas. |
| Revisão de telas | 47 páginas, claro/escuro e texto ampliado; sem violações axe/transbordamento registrados. |
| Composição responsiva | 168 combinações, sem transbordamento registrado. |
| Android nativo | 83 testes JVM/Robolectric aprovados e build release assinado. |
| APK distribuído | 34.0.0, código 3400; mesmo certificado anterior; hash conferido. |
| Runtime publicado | Host não confiável recusado, cabeçalhos observados; readiness 503 e liveness 200 com banco indisponível. |
| Dependências NuGet | Nenhum pacote vulnerável apontado na consulta do grafo. |

As contagens têm sobreposição entre suítes e não representam cobertura percentual de código. O ambiente usou dados fictícios, sem entrega de mensagens reais. Relatórios e logs estão em `validacao-v34/`.

## Correção do gerador de certificado no Windows PowerShell

A criação RSA agora informa 3072 bits no construtor, sem atribuir `KeySize`, que é somente leitura no provedor CSP do Windows PowerShell. O gerador reutiliza pasta vazia, preserva um par de arquivos já criado e recusa pasta incompleta com arquivos. A validação executada em PowerShell Core/Linux cobre as duas ramificações, importação do PFX e leitura pelo backend .NET 10; a execução nativa em Windows PowerShell 5.1 foi adicionada à CI e não foi realizada neste ambiente. Veja `validacao-v34/certificado-powershell.json`.
