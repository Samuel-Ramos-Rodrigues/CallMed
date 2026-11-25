# Arquitetura técnica e diagramas de fluxo — CallMed V54.1 NEXT

## Decisão arquitetural

Preservar o monólito modular .NET 10, PostgreSQL/EF Core, Razor e o app Kotlin/Jetpack Compose. A base **V54.1** utiliza `src/CallMed.Web`, `src/CallMed.Application`, `src/CallMed.Domain`, `src/CallMed.Persistence`, `src/CallMed.Worker`, `mobile/android`. Os namespaces `MKSANCrud` são herança e **não devem ser renomeados sem migração planejada**. A arquitetura já possui locks PostgreSQL/índice único no domínio da agenda e métodos específicos de canais.

- **Apresentação:** controllers com autorização, Razor e APIs mobile; JS sem regras de elegibilidade clínicas próprias.
- **Aplicação:** agenda, encaixe, consultas, jornada, atendimento, IA, teleconsulta, integração e métricas.
- **Persistência:** `MKSANContext`, migrations existentes, Identity, auditoria e eventos PEP.
- **Worker:** rotinas recorrentes com idempotência/retentativas, compartilhando serviços de domínio com a Web.
- **Android nativo:** autentica e usa `/api/mobile/v1`; não acessa PostgreSQL diretamente.

## 1. Arquitetura geral

```mermaid
flowchart TD
    P[Paciente navegador] --> W[CallMed Web MVC e APIs]
    R[Recepção / Admin / Profissional] --> W
    A[App Kotlin / Compose] --> M[API mobile v1]
    M --> W
    W --> S[Serviços de aplicação]
    K[Worker .NET] --> S
    S --> D[Persistência EF Core]
    D --> DB[(PostgreSQL)]
    S --> C[Conectores: Gmail / Brevo / Evolution / httpSMS]
    S --> AI[Gemini administrativo]
    S --> T[Teleconsulta WebRTC / sinalização]
    S --> F[PEP / FHIR administrativo]
```

## 2. Entidade-relacionamento conceitual (subconjunto, não DDL)

```mermaid
erDiagram
    PACIENTE ||--o{ CONSULTA : agenda
    PROFISSIONAL ||--o{ CONSULTA : atende
    PROFISSIONAL ||--o{ DISPONIBILIDADE : disponibiliza
    CONSULTA ||--o| JORNADA : acompanha
    CONSULTA ||--o{ SESSAO_TELE : participa
    CONSULTA ||--o{ PEP_EVENTO : produz
    PACIENTE ||--o{ LISTA_ESPERA : solicita
    LISTA_ESPERA ||--o{ OFERTA : recebe
    PACIENTE ||--o{ CONVERSA_ATENDIMENTO : usa
    CONVERSA_ATENDIMENTO ||--o{ MENSAGEM_ATENDIMENTO : contem
    CONSULTA ||--o{ REGISTRO_PSICOLOGICO : relacionado
    PROFISSIONAL ||--o{ REGISTRO_PSICOLOGICO : autoriza
```

*O desenho representa entidades relacionadas existentes; não substitui o mapeamento completo do EF (46 declarações DbSet, incluindo aliases; não são necessariamente 46 tabelas). Verificar nomes de FK e cardinalidades no `MKSANContext.cs` antes de qualquer alteração de esquema.*

## 3. Agendamento e confirmação

```mermaid
flowchart TD
    A[Paciente escolhe especialidade e profissional] --> B[Buscar disponibilidade autorizada]
    B --> C{Vaga válida e elegível?}
    C -->|Não| E[Mostrar alternativas sem criar consulta]
    C -->|Sim| F[Transação com bloqueio / verificação de conflito]
    F --> G{Reserva gravada?}
    G -->|Não| E
    G -->|Sim| H[Consulta persistida + evento operacional]
    H --> I[Worker prepara lembrete]
    I --> J[Canal configurado e autorizado]
    J --> K{Resposta verificada?}
    K -->|Confirma| L[Atualizar consulta com autorização]
    K -->|Cancela| M[Cancelar e disponibilizar vaga]
    K -->|Ambígua ou falha| N[Manter pendente / equipe]
```

## 4. Encaixe inteligente por oferta temporária

```mermaid
flowchart TD
    C[Cancelamento / vaga nova] --> E[EncaixeService identifica elegíveis]
    E --> F[Verificar profissão, modalidade, cobertura, horários e prioridade objetiva]
    F --> G[Reservar oferta com restrição e expiração]
    G --> H[Notificar por canal autorizado]
    H --> I{Aceite válido antes do prazo?}
    I -->|Não / expirada| J[Registrar resultado e considerar próximo]
    I -->|Sim| K[Transação / lock da vaga]
    K --> L{Disponibilidade continua livre?}
    L -->|Não| J
    L -->|Sim| M[Gravar consulta, consumir oferta, auditar]
```

*A presença de índice único `Oferta(MedicoId, Data, Horario)` para ofertas abertas e locks não substitui teste de concorrência com PostgreSQL. Nunca usar LLM para escolher prioridade clínica.*

## 5. Jornada do paciente e check-in

```mermaid
flowchart LR
    A[Agendado] --> B[Confirmação]
    B --> C[Dados cadastrais / convênio]
    C --> D[Formulários necessários]
    D --> E[Check-in digital]
    E --> F[Recepção verifica chegada real]
    F --> G[Atendimento]
    G --> H[Conclusão e indicadores]
```

*Check-in digital não é prova automática de presença física.*

## 6. Teleconsulta e perdas de conexão

```mermaid
flowchart TD
    A[Consulta online confirmada] --> B[Checar identidade e vínculo]
    B --> C[Preparação: consentimento / câmera / microfone]
    C --> D[Conectar via WebRTC]
    D --> E{Conectado?}
    E -->|Sim| F[Atendimento audiovisual]
    E -->|Falha| G[Orientação e tentativa limitada]
    F --> H{Rede caiu?}
    H -->|Sim| G
    H -->|Não| I[Encerrar sessão e registrar estado administrativo]
    G --> J{Retentativa autorizada?}
    J -->|Sim| D
    J -->|Não| K[Encaminhar equipe sem expor conteúdo clínico]
```

*Não há promessa de gravação automática nem de E2EE universal sem comprovação técnica.*

## 7. Atendimento multicanal e IA

```mermaid
flowchart TD
    A[WhatsApp / E-mail / SMS] --> B[Entrada idempotente]
    B --> C[Conversa, estado e histórico]
    C --> D{Requer equipe?}
    D -->|Sim| E[Fila humana]
    D -->|Não| F[IA apenas administrativa]
    F --> G{Revisão humana necessária?}
    G -->|Sim| H[Rascunho / aprovação]
    G -->|Não| I[Envio autorizado]
    H --> I
    I --> J{Status real do transporte}
    J -->|Entregue/enviado| K[Concluído]
    J -->|Fila/processamento| L[Enviando / aguardando confirmação]
    J -->|Falha| M[Falha / tratamento seguro]
```

**Correção desta entrega:** `AtendimentoEntregaEstado` centraliza a correspondência entre status de transporte e `IaEstado`; a tela da Central privilegia o status persistido da mensagem.

## 8. Privacidade e integração com prontuários

```mermaid
flowchart TD
    U[Usuário autenticado] --> B{Perfil + vínculo legítimo?}
    B -->|Não| X[403 sem limpar sessão]
    B -->|Sim| S[Serviço autorizado]
    S --> A[Auditoria de operação sensível]
    S --> E[Eventos administrativos PEP]
    E --> O[Outbox / adaptador externo autorizado]
    O --> T{Contrato e escopos válidos?}
    T -->|Não| R[Erro/retry controlado]
    T -->|Sim| P[PEP/FHIR administrativo]
```

Esta integração **não** é uma sincronização clínica universal de prontuários. Uma evolução multiempresa exige modelo de isolamento completo e testes antes de ativar tenants.

## Principais decisões de evolução

1. Não modificar tabelas nem migrations nesta rodada; manter rollback de código isolado.
2. Estados de entrega definidos no backend e confirmados pelo canal; frontend só apresenta o estado.
3. HTTP 401 expira sessão; HTTP 403 nega a operação preservando identidade (a API de refresh pode encerrar sessão em resposta de refresh inválido).
4. Manter invariantes clínicos no domínio, independentemente do canal ou aplicativo.
5. Priorizar testes PostgreSQL reais de concorrência antes de refatorar os locks existentes.
