# CallMed — teste real de encaixe automático em PostgreSQL

## Finalidade

Este teste adicional integra os serviços existentes do CallMed, com dois contextos EF Core e conexões PostgreSQL independentes na disputa de uma vaga. Não cria um simulador próprio da lógica do negócio.

**Escopo:** agendamento inicial -> inscrição de três pacientes na lista -> cancelamento via `AgendamentoService.CancelarAsync` -> oferta criada pelo `EncaixeService` -> tarefa de notificação para a recepção (sem provedores reais) -> aceite pelo agendamento real -> tentativa concorrente comum bloqueada -> oferta e lista persistidas -> tentativa repetida recusada -> oferta vencida recusada -> nova oferta ao próximo candidato.

## Atenção: banco exclusivo para testes

O novo teste executa `EnsureDeletedAsync()` para reiniciar **somente** o banco `callmed_test_encaixe` no host `127.0.0.1`. É proibido informar uma conexão de produção, uma cópia da base com dados reais ou outro banco. A execução é recusada caso o nome/host não correspondam exatamente ou falte a autorização explícita `CALLMED_TEST_RESET=SIM`.

1. Instale/inicie um PostgreSQL **local**, e crie o banco vazio `callmed_test_encaixe` (pelo pgAdmin ou por `CREATE DATABASE callmed_test_encaixe;`). Use credenciais locais com permissões para criação/remoção do banco de teste. Não use pacientes ou credenciais reais.
2. Na pasta raiz do projeto, abra um **PowerShell normal** (não é necessário o Package Manager Console) e configure a conexão. **Não compartilhe a senha ou a connection string preenchida em capturas de tela.**

   ```powershell
   $env:CALLMED_TEST_DB = 'Host=127.0.0.1;Port=5432;Database=callmed_test_encaixe;Username=postgres;Password=SENHA_LOCAL_APENAS_EXEMPLO'
   $env:CALLMED_TEST_RESET = 'SIM'
   dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj -c Release -- --encaixe-pg
   ```

3. Esperado: mensagens `PASS ENCAIXE-PG` e a linha final `ENCAIXE-PG: execução completa`. Qualquer exceção ou `FAIL` impede considerar aprovado. Cole o output **sem credenciais** para análise.
4. Após o teste, remova as variáveis da sessão PowerShell:

   ```powershell
   Remove-Item Env:CALLMED_TEST_DB
   Remove-Item Env:CALLMED_TEST_RESET
   ```

## Verificações e limites

| Evidência | O que deve comprovar |
|---|---|
| Agendar com serviço real | Consulta original confirmada e persistida |
| Cancelar com serviço real | Estado cancelado e oferta automática para a primeira prioridade |
| Reprocessamento | Não há duplicidade de oferta ativa |
| Notificação sem transporte | Tarefa de telefonia para recepção; **não** afirma WhatsApp/SMS entregue |
| Conexões concorrentes | Apenas aceite autorizado vence; tentativa comum perde |
| Banco após corrida | Uma consulta ativa por slot, oferta `Aceita` e lista finalizada |
| Replay | Mesmo UUID não pode consumir a oferta duas vezes |
| Expiração | Oferta vencida não confirma consulta; próximo candidato pode ser ofertado |

O teste **não comprova** mensagens reais entregues, chamadas HTTP autenticadas, execução temporizada do worker, performance em alta carga, concorrência entre vários processos de trabalho em máquinas distintas ou funcionamento do Render. Esses itens continuam exigindo homologação específica. Os dados são 100% fictícios.

## CI automatizado

Um job próprio `encaixe-postgres` foi adicionado a `.github/workflows/callmed-quality.yml`. Ele usa `postgres:17` efêmero, segredo de teste sem uso em produção, SDK .NET 10 e executa este mesmo teste ao integrar o código no repositório.

**Status da execução nesta entrega:** teste adicionado e conferido estaticamente; execução .NET/PostgreSQL **pendente** neste ambiente, que não disponibiliza essas ferramentas. Não se deve registrar `PASS` sem o resultado real do comando.
