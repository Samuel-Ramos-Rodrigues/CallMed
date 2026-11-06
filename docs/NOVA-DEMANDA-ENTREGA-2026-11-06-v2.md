# Entrega completa da nova demanda SAGA SENAI — 06/11/2026 (v2)

Substitui a entrega anterior. Base: CallMedCrud do ZIP "Atendimentos Separados".
Todos os recursos são gratuitos. Nenhum serviço pago foi adicionado.

## Situação por item da demanda

| Item | Entregue nesta versão | Ainda não atendido |
|---|---|---|
| Confirmação automática por WhatsApp e SMS | Lembretes com links de um toque para confirmar e cancelar (validade de 36 horas) | Não é possível confirmar por e-mail, por regra existente |
| Fila de espera por prioridade ou interesse | Campo de prioridade (0 a 99) definido pela equipe; notificação ordenada por prioridade e depois por data | — |
| Encaixe automático com confirmação | Ao liberar vaga, a mensagem traz "Aceitar com um toque"; o paciente confirma e a consulta é criada | Reserva automática sem nenhum clique do paciente (decisão: manter confirmação) |
| Check-in digital | Paciente ou recepção registra a chegada, a partir de 2 horas antes | Aviso em tempo real à recepção |
| Pré-cadastro, anamnese e dados de convênio e pagamento | Formulário enviado por link na véspera (24 horas antes); a equipe vê as respostas na consulta | Atualização automática do cadastro do paciente com os dados do convênio |
| Salas de atendimento | Cadastro de salas e definição por consulta, com bloqueio de conflito no mesmo horário | Gestão de salas pelo perfil Admin separado |
| Telemedicina | Modalidade Online, consentimento do paciente, sala virtual com nome não adivinhável e janela de acesso | Adequação completa à Resolução CFM 2.314/2022 (ver abaixo) |
| API para prontuários | Leitura de Patient e Appointment em HL7 FHIR R4 (`/api/fhir/r4`), com chave de integração | Escrita, recursos Practitioner e Location, autenticação SMART |
| Controle de acesso por perfil | Já existia; novas ações usam os mesmos perfis | Auditoria de cada endpoint |
| LGPD e criptografia | Auditoria registra ações de consulta, pré-cadastro e consentimento | Não auditado; criptografia de ponta a ponta não implementada (decisão de escopo pendente) |
| Acessibilidade | Já existia (Modo Fácil, texto ampliado, alto contraste) | Validação WCAG 2.2 |
| CFM e ANS | — | Análise regulatória pendente |

## Configuração necessária

- `Atendimento:Email:PortalBaseUrl`: URL https pública. Sem ela, as mensagens saem sem os links.
- `Telemedicina:Segredo`: texto secreto e longo. Sem ele, a sala virtual fica indisponível.
- `Telemedicina:BaseUrl`: padrão `https://meet.jit.si`. Recomendado: Jitsi Meet autohospedado, porque dados de saúde não devem circular em serviço público de terceiros.
- `LegacyIntegration:ApiKey`: chave da API de integração (usada também pela FHIR).
- `Database:ApplyV31Patch` e `Database:ApplyV32Patch`: padrão `true`. Aplicam as colunas e tabelas novas na inicialização.

## Alterações de banco (patches automáticos)

- V31: `Consultas.CheckInEm`.
- V32: `Consultas.Modalidade`, `Consultas.SalaId`, `Consultas.TelemedicinaConsentimentoEm`, `ListasEspera.Prioridade`, tabelas `Salas` e `PreConsultasFormularios`.

Ambos são idempotentes (`IF NOT EXISTS`). Não há migração do EF Core, seguindo o padrão V12 a V30 do projeto.

## Pontos legais que exigem análise antes de uso real

- **Telemedicina (CFM 2.314/2022):** o sistema registra consentimento, modalidade e sala. Ainda faltam: identificação do profissional, registro formal da consulta no prontuário, plano para emergências e regras de sigilo do ambiente de vídeo.
- **Pré-cadastro:** contém dados de saúde (alergias, medicamentos, histórico). Exige base legal e política de retenção definidas pela clínica.
- **Criptografia de ponta a ponta:** não implementada. Em aplicação web, o padrão é TLS em trânsito e criptografia em repouso. Ponta a ponta exigiria cifrar o conteúdo no aplicativo, o que impede a IA e as notificações de lerem o conteúdo.

## Verificação

- Não compilado nem executado: o ambiente não tem o .NET SDK.
- Verificações feitas: equilíbrio de chaves e parênteses em todos os arquivos alterados; conferência de que cada componente novo está registrado (DI, DbContext, patches e rotas).
- Antes de publicar: `dotnet build`; subir em homologação com o banco; testar, nesta ordem:
  1. Lembrete com links: link válido, link expirado, link adulterado.
  2. Lista de espera: prioridade alta sai primeiro; aceite por link cria a consulta e marca o item como encerrado.
  3. Pré-cadastro: envio pelo link, visualização pela equipe, bloqueio após cancelamento.
  4. Sala: conflito no mesmo horário bloqueado.
  5. Telemedicina: consentimento obrigatório antes da sala aparecer; sala abre 15 minutos antes.
  6. FHIR: `GET /api/fhir/r4/Appointment/{id}` com e sem a chave.
