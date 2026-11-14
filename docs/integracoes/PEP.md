# PEP — contrato administrativo V24

A API legada `/api/integracao/v1` é preservada. A nova integração é independente e desabilitada por padrão. Base: `/api/pep/fhir/R4`. Cabeçalho: `X-CallMed-PEP-Key`. HTTPS na implantação.

| GET | Conteúdo |
|---|---|
| `/metadata` | CapabilityStatement e recursos de leitura disponíveis, formato FHIR 4.0.1. |
| `/Patient/{id}` | ID interno, ativo, nome e nascimento. Sem CPF, telefone ou respostas clínicas. |
| `/Practitioner/{id}` | ID, nome e CRM cadastrado. |
| `/Appointment/{id}` | Horário UTC, estado e referências do paciente/profissional. |
| `/Encounter/{id}` | Etapa, modalidade, início e fim quando registrados. |
| `/Location/{id}` | Sala e situação. |

Subconjunto administrativo de leitura, sem SMART/OAuth, buscas globais, importação clínica, transações FHIR ou TISS. O fornecedor deve mapear IDs e validar o contrato. O segredo é de uma integração administrativa autorizada pela clínica, não de um paciente. Leituras são auditadas.

Mudanças da consulta geram eventos na mesma persistência. Webhook em CloudEvents JSON contém `id`, `type`, `time`, `source` e `data` com IDs administrativos, status/data/horário do evento. Não envia respostas clínicas.

- `Idempotency-Key`: UUID estável; deduplicar no receptor.
- `X-CallMed-Timestamp`: Unix em segundos; verificar janela de tempo acordada.
- `X-CallMed-Signature`: `sha256=` mais HMAC-SHA256 hexadecimal de `timestamp + "." + corpo UTF-8 exato`; comparar em tempo constante.

Entrega pelo menos uma vez, até dez tentativas, com espera exponencial limitada a seis horas. 2xx marca entrega. Uma queda após o aceite do receptor pode repetir o evento. O receptor deve deduplicar e tratar a ordem dos eventos por horário/ID. Não há promessa de entrega exatamente uma vez.

Eventos e tentativas ficam em `PepEventos`; `EntregueEm` indica entrega, `TentarEm` a próxima tentativa, dez tentativas sem entrega exigem análise operacional. O conector só envia com integração habilitada, URL HTTPS e segredo válidos. Não há credencial real ou integração com fornecedor ficticiamente ativada neste pacote.

Referência: [segurança FHIR R4](https://hl7.org/fhir/R4/security.html). Usar o formato FHIR não substitui autenticação, autorização, auditoria ou homologação do fornecedor.
