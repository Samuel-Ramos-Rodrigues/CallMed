# Matriz de convênios e verificação de elegibilidade — CallMed

## Referência adotada

O fluxo segue a prática administrativa de prestadores que consultam a elegibilidade do beneficiário e a situação cadastral no autorizador da operadora, como orientado pelo Pró-Saúde/Câmara dos Deputados. A ANS orienta a conferência do tipo de plano, rede credenciada, abrangência geográfica e cobertura contratada.

Referências públicas consultadas em 28/10/2026:

- ANS, **O que o seu plano de saúde deve cobrir?** https://www.gov.br/ans/pt-br/assuntos/consumidor/o-que-o-seu-plano-de-saude-deve-cobrir-1
- ANS, **Direitos do beneficiário** https://www.gov.br/ans/pt-br/publicidade-ans/direitos-do-beneficiario/direitos-do-beneficiario
- Pró-Saúde/Câmara dos Deputados, **Como devo proceder para verificar a elegibilidade do beneficiário?** https://prosaude.camara.leg.br/faq-items/como-devo-proceder-para-verificar-a-elegibilidade-do-beneficiario/

A matriz CallMed é uma referência operacional interna: não é uma matriz oficial de operadora, não contém coberturas presumidas e não consulta automaticamente a operadora. As regras devem ser alimentadas pela clínica a partir do contrato, do manual de credenciamento ou do portal da operadora, identificando produto/plano e unidade quando necessário.

## Regras implementadas

1. **Agendamento separado da autorização financeira:** o agendamento direto confirma a vaga assim que o horário é reservado. Pendências administrativas do convênio não mandam o agendamento para a triagem e não bloqueiam a reserva.
2. **Convênio válido:** para escolher convênio, o paciente precisa ter nome, cadastro e validade adequados. Isso não garante elegibilidade individual em tempo real.
3. **Sem regra para a combinação exata:** a consulta é confirmada e recebe `PendenteValidacao`. A equipe confere produto/plano, elegibilidade ativa, rede e especialidade diretamente com a operadora antes do atendimento.
4. **Conferência manual necessária:** a consulta é confirmada, mas fica com estado `PendenteValidacao`.
5. **Matriz indica cobertura sem autorização prévia:** a consulta é confirmada e recebe `CoberturaNaMatriz`; a equipe ainda precisa confirmar a elegibilidade individual junto à operadora.
6. **Autorização prévia exigida:** a consulta é confirmada para preservar o horário, recebe `AguardandoAutorizacao` e a equipe acompanha o protocolo antes da realização.
7. **Não coberta:** o agendamento usando o convênio como pagamento é recusado; o paciente pode selecionar atendimento particular.
8. **Revisão pela equipe:** em `Consulta > Detalhes`, funcionário ou administrador pode registrar `Validada`, `AguardandoAutorizacao` ou `NaoCoberta`. Observação é obrigatória e a decisão fica na auditoria. Se não houver cobertura, o horário é preservado e a forma de pagamento muda para particular; a equipe deve informar o paciente.
9. **Solicitações que passam pela triagem:** casos sem regra, marcados para conferência manual, com cobertura indicada pela matriz ou aguardando autorização permanecem pendentes até que a equipe registre uma liberação manual justificada após verificação externa. Quando a regra exige autorização prévia, o registro da elegibilidade como validada exige um protocolo. A não cobertura explícita não pode ser contornada como convênio; deve-se escolher particular ou corrigir a regra a partir de fonte confiável.
10. **Lista de espera automática:** só aceita uma combinação que tenha uma regra específica de matriz `Coberta`, sem exigência de autorização pendente. Casos incertos precisam de revisão manual antes de entrar no processamento automático.
11. **Migração preserva histórico:** consultas antigas que usam convênio recebem `PendenteValidacao`; particulares recebem `NaoAplicavel`. Regras antigas da matriz são migradas a partir do booleano antigo `Coberta`.

## Como configurar a matriz

1. Acesse **Administração → Convênios e cobertura**.
2. Cadastre a combinação real de operadora/produto e especialidade.
3. Selecione `Coberta conforme credenciamento`, `Não coberta` ou `Conferência manual necessária`.
4. Marque `Exige autorização prévia` quando a regra aplicável exigir senha/autorização.
5. Preencha observação com a fonte e a data de conferência.
6. Revise quando houver mudança contratual. Não use uma lista de cobertura de outra clínica como se fosse o contrato da CallMed.

## Banco de dados

O patch idempotente `DatabaseSchemaV25Initializer` adiciona estado da matriz, exigência de autorização e rastreio da elegibilidade por consulta. É aplicado no startup por padrão; `Database__ApplyV25Patch=false` desabilita a execução. Faça backup antes de implantar em produção.
