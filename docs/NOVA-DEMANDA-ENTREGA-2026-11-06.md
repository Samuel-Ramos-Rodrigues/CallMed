# Entrega da nova demanda SAGA SENAI — 06/11/2026

Base: CallMedCrud atual (ZIP "Atendimentos Separados"). Auditoria completa: `docs/AUDITORIA-SAGA-SENAI-2026-11-06-v2.md`.
Todas as alterações usam apenas recursos gratuitos (sem serviço pago).

## Incluído nesta entrega

1. **Check-in digital.** Paciente ou recepção registra a chegada na tela da consulta. Vale para consultas confirmadas ou remarcadas, a partir de 2 horas antes do horário, uma vez por consulta. Coluna `CheckInEm` (patch V31, automático). Indicador "Check-in digital" no painel de relatórios.
2. **Confirmar ou cancelar com um toque.** Os lembretes de WhatsApp e SMS passam a incluir dois links assinados e com validade de 36 horas: "Confirmar com um toque" e "Cancelar pelo link". Os links usam ASP.NET Data Protection, sem tabela nova. O GET só mostra a situação; a ação acontece num POST com antiforgery. Os links só aparecem quando `Atendimento:Email:PortalBaseUrl` (ou `RENDER_EXTERNAL_URL`) é uma URL https. E-mail não recebe links, de acordo com a regra de lembrete já existente.
3. **Auditoria** das ações feitas pelo link ("Confirmar" e "Cancelar", com descrição indicando o link). Cancelamento pelo link também encerra a solicitação e libera a vaga para a lista de espera.

## Ainda pendente (ordem sugerida)

1. Lista de espera com prioridade (campo novo) e reserva automática da vaga com confirmação do paciente.
2. Pré-cadastro, anamnese e envio prévio de convênio e pagamento.
3. Salas de atendimento e telemedicina (exige análise da Resolução CFM 2.314/2022).
4. Auditoria de LGPD, criptografia (ponta a ponta exige decisão de escopo) e API para prontuários com padrão.

## Verificação

- Não compilado nem testado: o ambiente não tem o .NET SDK. Rodar `dotnet build` e testar em homologação.
- Testes sugeridos: check-in numa consulta confirmada no dia; lembrete com link válido, link expirado e link adulterado; cancelamento pelo link libera a vaga.
