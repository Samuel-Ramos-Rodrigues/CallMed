# Alterações de agendamento e triagem de exames

## Agendamento de consultas

- O fluxo direto de agendamento cria a consulta como `Confirmada` e define `ConfirmadaEm` no momento da gravação. A tela informa que a consulta é confirmada após validação da disponibilidade e da forma de pagamento.
- O paciente autenticado não é mais redirecionado para uma solicitação de triagem apenas porque o convênio está incompleto, vencido ou não tem matriz de cobertura. Para convênio inválido, o formulário informa que o atendimento será particular; para convênio válido, a escolha de convênio/particular fica disponível.
- Se não houver matriz de cobertura cadastrada para um convênio, o agendamento direto não é bloqueado apenas pela falta dessa matriz. Se houver uma regra explícita indicando que a especialidade não é coberta, o sistema não permite usar aquele convênio nessa consulta e informa que o paciente deve optar por atendimento particular ou outra especialidade.
- A validação de horário disponível, os bloqueios transacionais PostgreSQL, a proteção contra consulta duplicada e os índices únicos permanecem ativos.
- A fila de solicitações/triagem permanece para pedidos administrativos recebidos por outros canais (por exemplo, telefone/presencial e contingência); o autoagendamento pela tela normal não passa mais por essa triagem.

## Histórico de exames

- Novos exames cadastrados pela equipe são persistidos como `Pendente` e ficam fora do histórico visível até serem aprovados.
- Funcionários e administradores acessam `HistoricoExames/Pendentes` pelo menu `Triagem de exames`, podendo aprovar (com observação opcional) ou rejeitar (com motivo obrigatório).
- Toda decisão registra data/hora, usuário responsável e evento de auditoria. Registros rejeitados são preservados no banco, mas não aparecem ao paciente nem na lista de exames aprovados.
- Registros existentes antes desta alteração são marcados como `Aprovado` pelo patch V24 para preservar o histórico já disponível.
- Esta é uma triagem administrativa de cadastro, não uma interpretação clínica ou liberação médica do exame.

## Campo de médico

- A expressão exibida `Médico preferido` foi substituída por `Médico de preferência` nos formulários de solicitação e triagem.

## Atualização do banco

O inicializador idempotente `DatabaseSchemaV24Initializer` adiciona as colunas de triagem de exames e o índice. É chamado pelo startup após V23. A configuração padrão é `Database:ApplyV24Patch=true` (`Database__ApplyV24Patch=true` no Render). Nenhum exame anterior é apagado.

## Testes incluídos

- Agendamento com convênio válido sem matriz deve confirmar a consulta e conservar o convênio cadastrado.
- Regra explícita de não cobertura deve impedir faturar a consulta pelo convênio, mas permitir seguir como particular.
- Convênio vencido não deve ser aceito como forma de pagamento.
- Histórico só mostra exames aprovados, e a fila de triagem só mostra registros ativos pendentes.

## Homologação recomendada antes de publicar

1. Execute `dotnet restore CallMed.sln` e `dotnet build CallMed.sln --configuration Release`.
2. Execute `dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj`.
3. Em homologação, teste agendamento em horários livres e ocupados, paciente com convênio válido sem matriz, regra de não cobertura com seleção particular, convênio vencido e permissões de triagem dos exames.
4. Confirme que `Database__ApplyV24Patch=true` está definido no Render; veja os logs de startup para confirmar que o patch foi aplicado.
