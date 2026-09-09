# CallMed — solução do desafio de marcação de consultas

## Objetivo

Tornar o processo híbrido de marcação de consultas mais rápido, acessível, padronizado e integrado sem excluir pacientes que dependem de telefone ou atendimento presencial.

**Posicionamento:** **Todos os canais. Uma única agenda.**

## Fluxo

```text
Paciente solicita
      ↓
PWA/Site | WhatsApp | Telefone | Presencial | E-mail | SMS
      ↓
Solicitação de atendimento
      ↓
Triagem administrativa
      ↓
Paciente + Convênio + Especialidade + Regras
      ↓
Médicos e vagas disponíveis
      ↓
Agendamento
      ↓
Confirmação / Lembretes
      ↓
Confirmar | Remarcar | Cancelar
      ↓
Cancelamento → Lista de espera
      ↓
Consulta / Ausência / Histórico / Indicadores
```

## Como a solução responde ao desafio

- **Agilidade:** centraliza solicitações e disponibilidade em um único fluxo.
- **Experiência do paciente:** PWA simples, informações claras e lembretes.
- **Acessibilidade:** texto maior, alto contraste, redução de animações, leitura por voz e Modo Fácil.
- **Inclusão:** telefone e presencial continuam disponíveis e entram no mesmo processo.
- **Absenteísmo:** confirmação, lembretes, cancelamento e lista de espera.
- **Visão integrada:** paciente, histórico, solicitações, consultas e atendimento em uma base única.
- **Padronização:** triagem, regras de convênio, status e auditoria.
- **Recursos humanos:** IA auxilia no primeiro contato e o humano assume quando necessário.
- **Baixa conectividade:** PWA com tela offline sem prometer reserva de vaga sem conexão.
- **Integração:** API protegida para HIS/agendas/cadastros legados.

## Fluxo de triagem

A triagem reúne paciente, especialidade, convênio, elegibilidade, preferências, observações e responsável. O servidor impede avançar para a agenda quando há pendências. Liberações excepcionais exigem justificativa e ficam auditadas.

## Agenda e lista de espera

A disponibilidade considera agenda semanal, exceções, bloqueios e consultas existentes. Em cancelamentos, a lista de espera pode reaproveitar a vaga após nova validação de elegibilidade e disponibilidade.

## Indicadores

Os relatórios priorizam métricas ligadas ao problema: solicitações pendentes, tempo até confirmação, taxa de confirmação, absenteísmo, vagas recuperadas e distribuição por canal.

## O que não faz parte do escopo

CallPoints, marketplace, clube de descontos, comunidade social e recursos de “super app” não fazem parte do escopo principal, pois não resolvem diretamente o desafio de marcação de consultas.
