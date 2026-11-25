# CHANGELOG e entrega — NEXT para CallMed V54.1

## Identificação do pacote

- **Base:** CallMed V54 de 15/11/2026 + V54.1 (botão Teleconsulta neutro). *Não foi feito merge no repositório remoto.*
- **Tema:** branco/verde `#0B5E4F`, cinco CSS modulares, cache `callmed-static-v105-teleconsulta-neutra`.
- **App no projeto:** `com.callmed.app`, APK Release **50.0.0 / 5000**, com tamanho e SHA-256 conferidos. Não recompilado/reassinado.
- **Banco:** sem migrations, alterações em tabelas ou modificações de dados.
- **Configuração:** nenhuma variável nova; integrações Gmail API, Brevo SMTP, Evolution API, httpSMS, Gemini e WebRTC preservadas.

## Arquivos-fonte modificados/adicionados

| Arquivo | Alteração |
|---|---|
| `src/CallMed.Application/Modules/Atendimento/Services/AtendimentoOrquestradorService.cs` | três caminhos agora persistem status de IA conforme resultado do transporte, não apenas conclusão da geração |
| `src/CallMed.Application/Modules/Atendimento/Services/AtendimentoEntregaEstado.cs` | **novo:** função única de mapeamento status de envio → estado persistido e resultado da retomada |
| `src/CallMed.Application/Modules/Atendimento/Services/AtendimentoIaStatus.cs` | falha de envio prevalece sobre progresso anterior; estados `enviando` explícitos |
| `tests/CallMed.Checks/AtendimentoRetomadaChecks.cs` | testes de regressão para confirmado, pendente, falha e rascunho |
| `mobile/android/app/src/main/java/com/callmed/app/CallMedViewModel.kt` | 403 deixa de provocar logout por interpretação equivocada |
| `mobile/android/app/src/main/java/com/callmed/app/SessionErrorPolicy.kt` | **novo:** política de sessão 401 distinta de autorização 403 |
| `mobile/android/app/src/test/java/com/callmed/app/SessionErrorPolicyTest.kt` | **novo:** testes JUnit da política |
| `tests/next/SessionErrorPolicySmoke.kt` | **novo:** teste Kotlin compilável sem Android SDK |
| `tests/next/check-transport-state.cjs` | **novo:** contrato estático dos caminhos de envio e política de sessão |
| `scripts/check-v35-classic-visual-contracts.py` | contrato antigo atualizado para a paleta V54 e cache v105, preservando check SVG |
| `scripts/auditar-next-atual.py` | **novo:** inventário reproduzível por módulo, incluindo checagem de APK/manifesto |
| `docs/next-generation/V54_1/*` | **novo:** auditoria, matriz, diagramas, indicadores, piloto, testes, implantação e pendências |
| `.github/workflows/callmed-quality.yml` | validação automática do novo contrato de estados e inventário em CI |
| `LEIA-ME-NEXT-V54.1.md` | **novo:** orientações de aplicação e compatibilidade |

## O que foi preservado integralmente

Controllers, Views Razor, cinco estilos CSS, regras da agenda, migrations, Worker, dados do banco, configurações de provedores, logo, tema V54.1 e APK Release V50 distribuído pelo site. Nenhuma linha de PHP, rota fictícia, mock de produção ou mudança de nome de tabelas foi adicionada.

## Pendências que bloqueiam a homologação

1. **Crítico:** compilar a solução .NET 10 e passar as suites `CallMed.Checks`; nesta entrega não havia SDK disponível.
2. **Crítico:** testes reais PostgreSQL de concorrência: dupla reserva, aceite de oferta expirada, repetição do webhook, queda de worker.
3. **Crítico:** testes de autorização por vínculo entre paciente, profissional, psicologia, formulários e registros; validar também Android 403.
4. **Alto:** gerar release Android atualizado e assinado com a chave V50 controlada localmente; substituir APK publicado somente após validar.
5. **Alto:** teste de entrega real/assinaturas de provedores, com credenciais em homologação, sem pacientes reais.
6. **Alto:** smoke test real do Render com quatro logins, status de IA, agenda, check-in, teleconsulta e disponibilidade dos canais.
7. **Médio:** auditoria visual autenticada em todas as 109 views; 16 observações do verificador estático continuam informativas.
8. **Médio:** medir fórmulas KPI com base consistente e períodos auditáveis; não preencher números demonstrativos como produção.
9. **Médio:** revisão jurídica/ética do tratamento de saúde/psicologia e processos de retenção, incidentes e consentimentos.
10. **Estratégico:** multiempresa ainda requer desenho de isolamento e migração futura planejada; não ativar tenancy parcial.

## Observação operacional

Não basta extrair este ZIP no ambiente de produção: o projeto contém fonte nova de Android ainda não representada pelo APK V50, e correções de estados que dependem de callback dos provedores. Primeiro valide em homologação, depois faça rollout com rollback disponível.
