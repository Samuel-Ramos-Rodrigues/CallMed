# Correções e validação — 16/11/2026

Base: ZIP CallMed-NEXT-V54.1-TESTE-ENCAIXE-Projeto-COMPLETO(1).zip fornecido nesta conversa.

## Falhas corrigidas

1. **Vagas presas por ofertas de pedidos ou pacientes inativos.** A limpeza do motor considerava somente expiração; a notificação só revogava ofertas com menos de cinco tentativas. Agora o motor revoga ofertas inelegíveis antes de selecionar novas vagas, independentemente das tentativas. A mesma execução pode oferecer a vaga ao próximo candidato.
2. **Duas ofertas para o mesmo pedido de espera.** O bloqueio original protegia somente profissional/dia/horário. Profissionais diferentes podiam selecionar simultaneamente um único pedido por especialidade. Acrescentado bloqueio transacional por pedido e releitura do pedido/paciente após a espera, com nova validação dos critérios.
3. **Aceite com oferta desatualizada no contexto do Entity Framework.** O controller carregava a oferta antes do bloqueio da vaga. Outra conexão podia alterar a oferta, e a consulta posterior reutilizava a entidade rastreada. Agora a oferta e seu pedido são relidos do PostgreSQL dentro da transação, antes de validar o aceite.
4. **Disponibilidade desativada durante a seleção.** O motor confirma novamente que a disponibilidade/profissional continuam ativos e que data e horário correspondem ao slot selecionado antes de criar a reserva. A duração usada no cálculo foi alinhada ao limite de 5–240 minutos já utilizado pelo serviço de agenda.

## Testes e entrega

- Compilação Release da solução .NET 10: **zero erros e zero avisos**.
- 15 suítes de domínio/aplicação, incluindo a nova `--encaixe-fixes`: **1.047 verificações aprovadas**.
- Sete suítes em **PostgreSQL 16.2 local, isolado, com dados sintéticos**: **65 verificações aprovadas**. Incluem migrações, salas, jornada, concorrência, cancelamento, ofertas e expiração.
- Total: **22 suítes e 1.112 verificações aprovadas**.
- A nova regressão de pedido inativo foi executada também com o serviço original: **falhou como esperado**, reproduzindo a vaga presa. Com a correção, passou.
- Testes adicionais PostgreSQL confirmam uma única oferta entre profissionais concorrentes e rejeição do aceite de entidade antiga alterada por outra conexão.
- Testes de contratos CSS, UX, estado do envio IA e arquitetura: aprovados.
- Sintaxe dos 26 JavaScripts: sem erros.
- Auditoria de 109 páginas Razor: 393 referências MVC e 57 referências de recursos verificadas; nenhuma referência quebrada detectada. A auditoria de UX mantém observações informativas de revisão visual, que não equivalem a erros confirmados.
- Publicação Release de Web e Worker validada; evidências em `validation/`.
- Regressão de pedidos/pacientes inativos adicionada ao workflow de qualidade.

## Limites da validação

Não foram usados banco, contas ou provedores de produção. Envio real por Gmail/Brevo/Evolution/httpSMS, configuração Render, testes visuais completos e reconstrução/assinatura do APK não foram executados nesta rodada. O APK incluído continua sendo o arquivo original. Os relatórios em `auditoria-integral/` são históricos; este documento e `validation/` registram esta rodada.

## Arquivos de implementação alterados

- `src/CallMed.Application/Modules/Agenda/Services/EncaixeService.cs`
- `src/CallMed.Application/Modules/Agenda/Services/AgendamentoService.cs`
- `tests/CallMed.Checks/EncaixeFixesChecks.cs` (novo)
- `tests/CallMed.Checks/EncaixeCancelamentoPostgresChecks.cs`
- `tests/CallMed.Checks/Program.cs`
- `.github/workflows/callmed-quality.yml`
