# Diagramas de dependências e fluxos (baseados em código localizado)

Estes diagramas são o mapa **inicial** dos fluxos. As arestas de negócio ainda requerem validação de execução. A única trilha corrigida foi envio IA → persistência do estado.

## 1. Arquitetura
```mermaid
flowchart TD
  Browser[Web Razor] --> Web[CallMed.Web]
  Android[Android Kotlin] --> Web
  Worker[CallMed.Worker] --> App[CallMed.Application]
  Web --> App
  App --> Persistence[CallMed.Persistence EF Core]
  Persistence --> DB[(PostgreSQL)]
  Persistence --> Domain[CallMed.Domain]
```

## 2. Relações de dados (resumo)
```mermaid
erDiagram
  Paciente ||--o{ Consulta : possui
  ProfissionalSaude ||--o{ Consulta : atende
  Consulta ||--o| Jornada : prepara
  Consulta ||--o{ SessaoTele : permite
  ListaEspera ||--o{ Oferta : recebe
  Consulta ||--o{ PepEvento : gera
  ConversaAtendimento ||--o{ MensagemAtendimento : contem
```

## 3. Agendamento
```mermaid
flowchart TD
  A[Paciente escolhe horário] --> B[AgendamentoService]
  B --> C[Validar horário, profissional e convênio]
  C --> D[Bloquear slot / transação]
  D --> E{Disponível?}
  E -->|Sim| F[Registrar Consulta e PepEvento]
  E -->|Não| G[Recusar e sugerir nova busca]
```

## 4. Confirmação multicanal
```mermaid
flowchart TD
  A[Consulta agendada] --> B[Lembrete e link]
  B --> C[Provedor autorizado]
  C --> D{Resposta válida?}
  D -->|Sim| E[Atualizar estado consulta]
  D -->|Não| F[Manter pendente e registrar tentativa]
```

## 5. Encaixe inteligente
```mermaid
flowchart TD
  A[Horário disponível] --> B[EncaixeService.ReservarAsync]
  B --> C[Lock PostgreSQL e elegibilidade]
  C --> D{Ainda livre?}
  D -->|Sim| E[Oferta com expiração]
  D -->|Não| F[Não criar reserva]
  E --> G{Aceite antes da expiração?}
  G -->|Sim| H[AgendamentoService transacional]
  G -->|Não| I[Expirar / próximo candidato]
```

## 6. Jornada do paciente
```mermaid
flowchart TD
  A[Consulta] --> B[Confirmação]
  B --> C[Pré-cadastro e formulário]
  C --> D[Check-in digital]
  D --> E[Recepção valida chegada]
  E --> F[Atendimento]
  F --> G[Histórico/indicadores]
```

## 7. Teleconsulta
```mermaid
flowchart TD
  A[Consulta online] --> B[Verificar acesso paciente/profissional]
  B --> C[Sala e ICE/STUN/TURN]
  C --> D[WebRTC]
  D --> E{Sessão concluída?}
  E -->|Sim| F[Registro administrativo]
  E -->|Não| G[Revalidar/reconectar]
```

## 8. Atendimento e IA
```mermaid
flowchart TD
  A[WhatsApp/SMS/Gmail] --> B[Webhook ou polling]
  B --> C[Persistir mensagem e deduplicar]
  C --> D[AtendimentoOrquestradorService]
  D --> E[Resposta administrativa / revisão]
  E --> F[AtendimentoEnvioService]
  F --> G[Status do provedor]
  G --> H[AtendimentoEntregaEstado.Persistir]
  H --> I[Estado na Central]
```
