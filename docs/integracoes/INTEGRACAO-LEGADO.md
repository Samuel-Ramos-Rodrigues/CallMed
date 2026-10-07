# CallMed V21 — Integração com sistemas legados

A API só é habilitada quando `LegacyIntegration__ApiKey` é configurada no ambiente.
Envie a mesma chave no header `X-CallMed-Integration-Key`.

Endpoints:
- `GET /api/integracao/v1/status`
- `GET /api/integracao/v1/pacientes/cpf/{cpf}`
- `GET /api/integracao/v1/disponibilidade?especialidade=...`
- `POST /api/integracao/v1/solicitacoes`

A API é propositalmente pequena: permite que um HIS/cadastro/agenda legado consulte dados administrativos essenciais e injete uma solicitação no mesmo fluxo omnichannel da CallMed, sem duplicar a regra de agenda.
