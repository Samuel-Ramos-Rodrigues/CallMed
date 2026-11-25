# Indicadores operacionais e roteiro de demonstração SENAI

## Fonte, fórmula e critérios de cálculo

Nenhum número de exemplo abaixo é um resultado de clínica real. O código contém `MedicaoOperacional`, `MedicaoPiloto`, `Consulta`, `Oferta`, `Jornada`, `MensagemAtendimento` e `AuditoriaEvento`; a disponibilidade e granularidade histórica de cada denominador devem ser validadas no banco de teste antes de publicar um painel de KPI.

| Métrica | Definição desejada | Fonte possível | Ressalva |
|---|---|---|---|
| Absenteísmo | consultas elegíveis com falta / consultas previstas para ocorrer × 100 | `Consulta.Status`, histórico de atendimento | Excluir canceladas previamente; validar alterações retroativas |
| Confirmação | consultas confirmadas / consultas que exigiam confirmação × 100 | consulta + evento de confirmação | Não confundir entregue com confirmado |
| Tempo de confirmação | média(data_hora_confirmação − data_hora_convite) | eventos da comunicação/consulta | Pode exigir evento temporal adicional; não inferir de status atual |
| Ocupação | horas efetivamente reservadas / horas ofertadas elegíveis × 100 | disponibilidades, bloqueios e consultas | Descontar indisponibilidades válidas |
| Cancelamentos | quantidade de cancelamentos no período | histórico da consulta | Não somar status atual como evento histórico sem registro |
| Vagas recuperadas | vagas canceladas que viraram novas consultas vinculáveis | `Oferta`, `Consulta`, auditoria | Exigir relação inequívoca entre vaga original e nova reserva |
| Tempo de recuperação | média(reocupação − cancelamento) | eventos de alteração / oferta | Sem timestamps históricos não reportar média |
| Check-in digital | check-ins digitais válidos / consultas elegíveis | `Jornada`, consulta | Digital ≠ presença física |
| Espera na recepção | média(início atendimento − chegada confirmada) | eventos de check-in/atendimento | Somente se ambos os marcos existirem |
| Ações automatizadas | contagem de ações concluídas pelo worker/IA sem intervenção humana | `AuditoriaEvento`, conversas, eventos | Não contar tentativas como êxito |
| Entregas e falhas | entrega confirmada / mensagens enviadas, taxas de falha e envio incerto | `MensagemAtendimento.Status` | Separar aceitação, envio e entrega efetiva |
| Satisfação | média/percentual de avaliações obtidas entre elegíveis | `AvaliacaoAtendimento` | Apresentar tamanho da amostra |

**Regra de apresentação:** indicador inexistente = `Não disponível`, não zero. Permitir filtro por intervalo, modalidade e especialidade com validação dos denominadores. Dados reais: controle de acesso e agregação apropriada; evitar expor detalhes de atendimento psicológico no painel administrativo.

## Demonstração prática com fixture sintética (roteiro executável manualmente)

**Preparação segura:** banco PostgreSQL isolado, dois pacientes fictícios (`Paciente A`, `Paciente B`), um profissional fictício, disponibilidade hoje+2 dias, um slot de teleconsulta e canais simulados. Não executar no banco Render de produção. Registrar IDs internos apenas no ambiente de teste.

| Cenário | Ações na aplicação real | Evidência/critério |
|---|---|---|
| A. Agendamento + confirmação | Paciente A seleciona especialidade, profissional e horário; worker simula lembrete; paciente confirma pela ação autorizada | Uma única consulta, confirmação persistida, comunicação rastreável, sem mensagem real |
| B. Cancelamento + encaixe | Paciente A cancela; lista verifica Paciente B; cria oferta com expiração; B aceita | Uma vaga, uma consulta final; ofertas duplicadas/vencidas rejeitadas; log do evento |
| C. Check-in digital | B confere cadastro, preenche formulário e faz check-in; recepção valida chegada | Etapas distintas e cronologia; check-in remoto não marca presença física sozinho |
| D. Teleconsulta | profissional e B entram na sala em redes distintas, com permissões consentidas; simular desconexão | Estados de aguardo, conectando, reconectando e encerramento sem acesso cruzado |
| E. Indicadores | consultar período do teste e conferir eventos brutos | métricas calculadas sem valores fictícios disfarçados; falhas de envio separadas |

### Casos negativos obrigatórios

- Duas sessões tentam reservar a mesma vaga; somente uma é confirmada.
- Oferta expira durante aceite; banco impede confirmação.
- Webhook é reenviado; uma entrada e uma resposta autorizada, no máximo.
- Worker reinicia durante envio incerto; não duplicar mensagem sem confirmação.
- Paciente A solicita a consulta de B; autorização nega sem expor dados.
- Profissional sem vínculo tenta acessar registro clínico; negar e auditar.
- Banco indisponível durante check-in; mostrar erro e permitir retomada segura.
- Zoom do navegador em 200% e 400%, contraste e teclado nos quatro perfis.

**Critério de sucesso SENAI:** demonstrar uma mesma jornada real (não uma simulação separada) percorrendo serviço de negócio, persistência e UI, com conectores externos falsos controlados. Todos os logs e capturas devem usar identidades fictícias.
