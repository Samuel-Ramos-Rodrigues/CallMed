# CallMed Next Generation — auditoria e matriz funcional da V54.1

**Base:** `CallMed-V54-Projeto-COMPLETO-Verde-Fluxos-APK-V50.zip`, sobreposta pela correção `CallMed-V54.1-Corrigir-Botao-Teleconsulta.zip`. **Não é a V35.** Estado e código de 15/11/2026. Todos os nomes de pacientes de exemplos/testes devem ser sintéticos. Esta auditoria é predominantemente estática e não constitui certificação clínica, jurídica, de segurança ou homologação de produção.

## Método e limites

- Escopo de inventário: `src/CallMed.Web`, `src/CallMed.Application`, `src/CallMed.Persistence`, `src/CallMed.Domain`, `src/CallMed.Worker`, `mobile/android`, `tests`, `scripts`, `docs`.
- Execução local: validações de HTML/Razor por analisadores existentes, CSS, JavaScript, verificação da integridade do APK, política Kotlin isolada e contrato estático das correções.
- **Não executado:** `dotnet build`, testes .NET, PostgreSQL concorrente, Playwright nas telas reais autenticadas, conexões Gmail/Evolution/httpSMS/Brevo/Gemini, teleconsulta WebRTC entre dispositivos, Gradle assembleRelease. Nenhuma conta real foi usada. Nada foi alterado no banco/Render/GitHub.
- `scripts/auditar-next-atual.py` e `INVENTARIO-ESTATICO.json` geram a lista por módulo. 109 views Razor (incluindo 8 da área Identity), 54 arquivos de controllers/partials, 46 declarações `DbSet`, 91 ocorrências `HasIndex` em persistência, 26 JavaScript, 5 CSS e 19 Kotlin.

## Achados priorizados

| ID | Severidade | Evidência e arquivo | Impacto | Correção/estado | Prova necessária |
|---|---|---|---|---|---|
| N01 | Alta | `AtendimentoOrquestradorService.cs`: três caminhos atualizavam `IaEstado` para `concluida` independentemente de falha/processamento do canal | Equipe poderia interpretar mensagem como entregue | **Corrigido no código** com `AtendimentoEntregaEstado.cs` e regressões em `AtendimentoRetomadaChecks.cs` | .NET tests com canais simulados, falha e envio incerto |
| N02 | Alta | `CallMedViewModel.kt`: cinco condições tratavam 403 como 401 | Bloqueio por falta de permissão podia encerrar sessão válida | **Corrigido no código Kotlin** via `SessionErrorPolicy.kt`; teste isolado e JUnit adicionados | Gradle unit tests e aparelhos; **APK distribuído ainda contém binário pré-correção** |
| N03 | Média | `AtendimentoIaStatus.cs`: estado de IA em andamento podia se sobrepor à falha de envio já armazenada | Falha real aparecia como atividade | **Corrigido no código**: status do transporte tem precedência | Teste da Central autenticada, com callback de falha |
| N04 | Média | `scripts/check-v35-classic-visual-contracts.py` ainda exigia roxo V35/cache v103 enquanto V54.1 usa verde/cache v105 | CI falhava apesar de CSS correto | **Corrigido no script**, preservando validação de 47 símbolos SVG e 39 referências | Executado localmente; passar também na CI |
| N05 | Alta | `mobile/android` possui fontes V54 + V50 release já assinado por chave privada externa | Código atualizado não se reflete automaticamente no APK instalado | **Pendente**: compilar e assinar novo release com a chave do responsável | SHA-256/assinatura, instalação em Android, login e chamadas reais |
| N06 | Alta | `src/CallMed.Application/Modules/Agenda/Services/AgendamentoService.cs` (770 linhas), `MKSANContext.cs` e `EncaixeService` | Conflitos de reserva e estados incongruentes são críticos | Há índice único e advisory locks no código; **não reescrito sem evidência de falha** | Testes PostgreSQL com 2+ conexões concorrentes e falhas entre commits |
| N07 | Média | `AtendimentoEmailOptions` habilita resposta administrativa automática conforme configuração; existe rascunho manual | Expectativa de revisão humana varia por canal e política | **Não alterado sem decisão de produto**; requer separar claramente mensagem administrativa segura, revisão humana e alterações de agenda | Teste E2E Gmail/Brevo simulados + revisão das regras da clínica |
| N08 | Média | 16 observações informativas em `scripts/auditar-ux-paginas.py` (IDs condicionais, dados estilizados) | Possíveis regressões de DOM/contraste em estados alternativos | **Pendente**: inspecionar DOM real nos quatro perfis | Playwright desktop/mobile, zoom 200%, teclado, leitor de tela |
| N09 | Alta | `src/CallMed.Application/Modules/Teleconsulta`, `Jornada/Tele.cshtml`, `mobile/android/NativeTele.kt` | Disponibilidade de rede/consentimento e autorização são críticos | Implementação existente preservada; sem mudança não verificada em WebRTC | Dois dispositivos, redes diferentes, ICE/STUN/TURN, reconexão |
| N10 | Alta | Psicologia: `RegistroPsicologico`, `GuardaRegistroPsicologico`, `AutorizacaoResponsavel`, `ChaveClinica` e `Views/Psicologia` | Sigilo e acesso profissional; risco por erro de escopo | Controles presentes no código, **não validados juridicamente** | Testes de acesso cruzado e revisão especializada CFP/LGPD |
| N11 | Média | `ListaEsperaService` notifica apenas com retorno confirmado; `Oferta` índice único para estado aberta e `EncaixeService` | Etapas assíncronas e reprocessamentos exigem idempotência | Estrutura presente no código, sem nova automação fictícia | Testar oferta vencida, reenvio, concorrência e restart do worker |
| N12 | Média | Persistência/integração PEP FHIR parcial; `PepEvento` gerado por `PrepararOutbox()` | Integração externa pode não corresponder a PEP clínico completo | Preservada; não anunciada como interoperabilidade universal | Testes de contrato por adaptador autorizado, versão e autenticação |

## Matriz funcional (não confundir código presente com funcionalidade comprovada)

| Módulo | Evidência | Classificação nesta análise | Testes para comprovar |
|---|---|---|---|
| Login, permissões e conta | `Identity`, `MinhaConta`, `ContaPerfil`, `MobilePacienteController` | **Implementado, mas sem testes de sessão real nesta rodada** | Papéis, CSRF, recuperação e 401/403 |
| Agendamento e reagendamento | `AgendamentoService`, `ConsultaController`, views `Consulta` | **Implementado, sem teste executado** | Dupla reserva, janela de horário, cancelamento |
| Confirmações multicanal | `ConfirmacoesService`, SMS/WhatsApp/e-mail | **Implementado, sem teste executado** | Entrega, ação ambígua e token vencido |
| Fila de espera | `ListaEsperaService`, `EncaixeService`, `Oferta` | **Implementado, sem teste executado** | Convênios, expiração e corrida |
| Jornada/check-in | `Views/Jornada`, `Views/Checkin` | **Implementado, sem teste executado** | remoto ≠ presença física |
| Salas e teleconsulta | `Views/Salas`, `NativeTele`, sinalização WebRTC | **Implementado, sem teste de dispositivos** | 2 redes e reconexão |
| Atendimento humano e IA | `AtendimentoConversaService`, `AtendimentoOrquestradorService` | **Implementado com defeitos corrigidos no código; testes .NET pendentes** | envio incerto, deduplicação, equipe assume durante IA |
| Psicologia / registros | `Views/Psicologia`, `RegistroPsicologico` | **Implementado; privacidade não homologada** | vínculo, retificação e sigilo |
| Métricas/auditoria | `MedicaoOperacional`, `MedicaoPiloto`, `AuditoriaEvento` | **Implementado, cálculo e cobertura não auditados integralmente** | fórmulas, períodos e reprocessamento |
| Android nativo | `mobile/android`, Kotlin/Compose | **Implementado com correção de código não distribuída no APK atual** | Gradle, assinatura e casos no aparelho |
| PEP/FHIR administrativo | `PepEvento`, integração PEP/FHIR | **Parcial quanto à interoperabilidade clínica ampla** | contrato por sistema terceiro |
| Multiempresa comercial | não foi confirmado isolamento de dados por tenant nesta auditoria | **Não afirmar multi-tenant** | arquitetura de dados, segregação antes da comercialização |

## Priorização para o próximo ciclo de implementação

1. **Bloqueadores:** compilar .NET/Android e rodar regressões, validar reservas e ofertas com PostgreSQL real; testar autorização e conteúdo clínico por vínculo, não apenas papel.
2. **Operação:** medir entrega real das mensagens e recuperação de workers, homologar notificações com canais de teste.
3. **Piloto SENAI:** demonstrar agendamento→confirmação→cancelamento→oferta→check-in→teleconsulta→indicador, usando fixture sintética e os serviços reais.
4. **UX e acessibilidade:** testar telas reais de cada perfil, inclusive estados de falha, sem trocar a identidade verde da V54.1.

**Mudanças de banco:** nenhuma migration nova; esquema e índices existentes preservados. **Variáveis:** nenhuma nova nesta entrega. **Assinatura:** nenhuma chave `.jks` incluída.
