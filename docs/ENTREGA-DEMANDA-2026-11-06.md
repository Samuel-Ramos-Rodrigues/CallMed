# CallMed — Aplicação da nova demanda (base: CallMed original)

Data: 06/11/2026. Base: o projeto CallMed que já estava pronto (ZIP original). Sobre ele foram aplicadas as mudanças da demanda que faltavam, sem reconstrução.

## Estado: NÃO COMPILADO
Não há .NET SDK neste ambiente. Verificação feita: balanceamento de chaves e parênteses nos arquivos alterados, existência de todos os símbolos usados e registro de dependências. Antes de publicar: `dotnet build`, testes do `tests/` e o plano de homologação deste documento.

## Requisitos da demanda × situação

| Requisito da demanda | Situação | Onde está |
|---|---|---|
| Agendamento online 24/7 | Já existia | `ConsultaController.Create`, `MobilePacienteController` |
| Lembretes automáticos por WhatsApp e SMS | Já existia (24h e 2h) | `LembreteConsultaBackgroundService` |
| Confirmar com um clique | Já existia (link assinado) | `AcaoConsultaController` |
| Cancelar com um clique | Já existia (link assinado) | `AcaoConsultaController` |
| **Reagendar com um clique** | **NOVO** | Link "Remarcar pelo link" no lembrete; página `/consulta/reagendar/{token}` lista horários livres do mesmo médico nos próximos 14 dias; `AgendamentoService.RemarcarAsync` |
| Fila de espera por prioridade | Já existia, **corrigido** | `ListaEsperaService` |
| **Encaixe automático com reserva** | **NOVO (antes era aviso para todos)** | Cada vaga é oferecida a uma pessoa por vez, a de maior prioridade. A reserva vale 2 horas (`ListaEsperaService.JanelaReservaHoras`). Depois disso, a vaga passa ao próximo. `ListaEsperaAceiteService` recusa aceite fora do prazo |
| Check-in digital | Já existia (entrega anterior) | `AgendamentoService.RegistrarCheckInAsync` |
| Pré-cadastro e anamnese | Já existia, **reforçado** | `AcaoConsultaController.SalvarFormulario` |
| Convênio e pagamento antes da consulta | Já existia | Campos de `PreConsultaFormulario` |
| **Consentimento para uso dos dados (LGPD)** | **NOVO** | Caixa obrigatória no formulário; `ConsentimentoEm` |
| **Exportação dos dados pelo titular (LGPD)** | **NOVO** | `MeusDadosController.Exportar`: JSON com cadastro, consultas e pré-cadastro decifrado. Cada exportação fica na auditoria. Link em Privacidade, seção 7 |
| **Anonimização do titular (LGPD)** | **NOVO** | `LgpdController` (só Admin, em `/Lgpd`): troca nome e CPF por identificador, apaga contatos, convênio e protocolo, apaga o texto livre do pré-cadastro e das mensagens, desativa listas de espera e bloqueia o login. Mantém consultas e o registro clínico da telemedicina. Irreversível e auditada |
| **Contato do encarregado (LGPD, art. 41)** | **NOVO** | Página de privacidade usa `Lgpd:EncarregadoEmail` (cai para `PlayStore:SupportEmail`) |
| **Anamnese cifrada em repouso** | **NOVO** | `CampoSensivelService` (DataProtection, prefixo `enc:v1:`) |
| **Painel do fluxo do dia em tempo real** | **NOVO** | `/FluxoAtendimento` atualiza a cada 20 segundos. Médico vê só as próprias consultas. Nome reduzido na tela ("Maria S.") |
| Indicadores de absenteísmo, ocupação e recuperação de vagas | Já existia | `RelatoriosController` |
| Múltiplos profissionais | Já existia | `Medico`, `Especialidade` |
| Salas de atendimento | Já existia (entrega anterior) | `Sala`, `ConsultaController.DefinirSala` |
| Telemedicina (sala virtual) | **Parcial** | Link com HMAC, consentimento e janela de horário já existiam. **NOVO:** sala de espera (pelo detalhe da consulta, pelo portal e pelo link `/consulta/sala/{token}` do lembrete), admissão, início e fim pela equipe (`TelemedicinaEquipeController`), registro do atendimento cifrado. **Pendente:** revisão jurídica da Resolução CFM 2.314/2022 e teste com sala real |
| API para prontuários (PEP) | **Parcial** | Leitura FHIR R4 de `Patient` e `Appointment` já existia. **NOVO:** `GET /api/fhir/r4/metadata` (CapabilityStatement) e busca `GET /api/fhir/r4/Appointment?patient={id}` (Bundle searchset, até 100 itens). **NOVO:** OAuth2 client credentials (`POST /api/fhir/r4/oauth/token`, token de 1 hora) e especificação OpenAPI em `/api/fhir-openapi.json`. Autenticação aceita chave de integração ou Bearer |
| Controle de acesso por perfil | Já existia | Perfis Admin, Funcionario, Medico, Paciente. **Pendente:** revisão de autorização em cada endpoint |
| Criptografia ponta a ponta | **Decisão pendente** | Ver seção abaixo |
| Acessibilidade (WCAG 2.2) | Parcial | Modo fácil, texto ampliado e alto contraste já existiam. **Pendente:** teste com leitor de tela e auditoria automática |
| Regulamentações CFM e ANS | **Pendente** | Análise regulatória |

## Criptografia ponta a ponta: decisão a confirmar com o SENAI
Ponta a ponta de verdade (só o paciente e o médico leem o conteúdo) impede que o servidor, a IA e as notificações leiam o texto. Isso conflita com o próprio fluxo da demanda (lembretes, agente e painel).

Foi implementado o que é compatível com o fluxo:
- TLS em todo o trânsito.
- Anamnese cifrada no banco (`enc:v1:`), com chave gerida pelo DataProtection.

Confirmar com o SENAI se "ponta a ponta" significa isso.

## Migração do banco
Nova patch `DatabaseSchemaV33Initializer`:
- Adiciona `PreConsultasFormularios.ConsentimentoEm`.
- Converte os campos de texto do pré-cadastro para `text`, porque o texto cifrado é maior que o original.
- Adiciona em `Consultas` as colunas da sala de espera: `TelemedicinaSalaEsperaEm`, `TelemedicinaAdmitidoEm`, `TelemedicinaInicioEm`, `TelemedicinaFimEm` e `TelemedicinaRegistro` (cifrado).

`Paciente.Senha` é `[NotMapped]`: nunca foi gravada no banco, então não há senha em texto puro a limpar.

Controle: `Database:ApplyV33Patch` (padrão `true`). O patch é idempotente.

**Atenção:** registros de pré-cadastro já existentes ficam em texto puro. Eles continuam legíveis (o `CampoSensivelService` aceita valores sem prefixo), mas não ficam cifrados até serem preenchidos de novo.

## Plano de homologação
1. Compilar e rodar o `tests/`.
2. Reagendar por link: abrir o lembrete, escolher um horário livre, conferir a consulta com status `Remarcada`.
3. Reagendar por link em horário ocupado: a página deve recusar e listar de novo.
4. Fila: criar duas pessoas na lista do mesmo médico, abrir uma vaga. Só a de maior prioridade deve receber a oferta. Passadas 2 horas sem aceite, a segunda deve receber.
5. Aceitar depois de 2 horas: deve recusar com a mensagem de prazo.
6. Pré-cadastro sem marcar o consentimento: deve recusar. Com consentimento: salvar, e conferir no banco que o texto começa com `enc:v1:`.
7. Abrir a consulta na equipe: a anamnese deve aparecer em texto legível.
8. Fluxo do dia: com um usuário Médico, ver só as consultas dele. Com Funcionário, ver todas. Confirmar a atualização automática.
9. Abrir `/api/fhir/r4/metadata` e validar o JSON.
10. Rodar a migração V33 em uma cópia do banco de produção.
11. Telemedicina: paciente com consentimento entra na sala de espera. Antes da admissão, o link da sala não aparece. Equipe admite, inicia e encerra pelo painel `/TelemedicinaEquipe`. Conferir que `TelemedicinaRegistro` começa com `enc:v1:`.
12. Médico: no painel de telemedicina, ver só as consultas dele. Tentar admitir consulta de outro médico (pelo id na URL): deve recusar.
13. Exportação: como paciente, abrir `/MeusDados/Exportar`. O JSON deve trazer o pré-cadastro em texto legível, e a auditoria deve registrar a exportação.
14. FHIR: `GET /api/fhir/r4/Appointment?patient={id}` com a chave de integração devolve um Bundle `searchset`. Sem `patient`, devolve 400.
15. Sala pelo link: abrir o link de sala do lembrete antes de 15 minutos: deve recusar a entrada. Dentro da janela, entrar na espera. Antes da admissão, o link da sala virtual não aparece.
16. Anonimização: como Admin, anonimizar um paciente de teste sem digitar ANONIMIZAR: deve recusar. Depois, conferir no banco que o nome começa com `Titular anonimizado`, que o login não funciona e que as consultas continuam.
17. Encarregado: sem `Lgpd:EncarregadoEmail`, a página de privacidade usa o e-mail de suporte. Com a variável, usa o do encarregado.
18. OAuth FHIR: `POST /api/fhir/r4/oauth/token` com `grant_type=client_credentials` e as credenciais certas devolve `access_token`. Com credenciais erradas: 401. Usar o Bearer em `Appointment?patient={id}`: deve responder 200.
19. Anonimização completa: depois de anonimizar, conferir que o pré-cadastro do paciente não tem mais texto em queixa, alergias, medicamentos e histórico.
20. OpenAPI: abrir `/api/fhir-openapi.json` e validar o JSON com um validador de OpenAPI.

## Variáveis novas (rodadas anteriores e esta)
- `Lgpd:EncarregadoEmail`: e-mail do encarregado de dados, exibido na política de privacidade.
- `LegacyIntegration:ClientId` e `LegacyIntegration:ClientSecret`: credenciais do OAuth2 client credentials da API FHIR. Sem elas, o endpoint responde 503 e só a chave de integração funciona.
- `Atendimento__WhatsApp__Evolution__WebhookSecret` e `Atendimento__Sms__Http__WebhookSecret` (rodada de respostas).

## Pendências (fora desta entrega)
- **Criptografia ponta a ponta:** não implementada. Ponta a ponta de verdade exige cifrar no navegador, com chave que só o paciente e o médico têm; isso impede os lembretes, o agente e o painel de lerem o conteúdo, que são requisitos da própria demanda. Foi entregue cifragem em repouso (`enc:v1:`) e TLS. A decisão final é do SENAI.
- **Revisão de autorização de todos os endpoints:** não feita de forma completa. Foram checados os controladores de telemedicina, anonimização, exportação e FHIR.
- **Análise de conformidade com a ANS e revisão jurídica do CFM 2.314/2022 e do prazo de guarda do prontuário:** exigem profissional de direito e saúde, não são código.
- **Acessibilidade (WCAG 2.2):** teste com leitor de tela e ferramenta automática (axe) só pode ser feito em um navegador com o sistema rodando.
- **Compilação e testes:** não há .NET SDK neste ambiente. Tudo foi verificado só por estrutura. Rode `dotnet build` e `tests/` antes de qualquer uso.
