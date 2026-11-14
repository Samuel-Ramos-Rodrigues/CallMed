# Metered TURN — configuração do CallMed

Esta revisão aceita os campos `username` e `credential` fornecidos pelo Metered ou por outro provedor TURN compatível com WebRTC. Também preserva coturn com segredo compartilhado. Não precisa recompilar o APK 24.0.0 nem alterar o banco.

## Obter a credencial

1. No [painel Metered](https://dashboard.metered.ca), use **Servidor TURN e SFU → Credenciais**, ou **Crie sua primeira credencial** na visão geral.
2. Crie uma credencial TURN. Se houver um campo de identificação/Label, pode usar `CallMed`.
3. Na credencial criada, abra **Get credential → Show ICE Servers Array**. Os nomes podem aparecer traduzidos pelo navegador. Copie os endereços exatamente como fornecidos; domínio e região podem variar.
4. O conjunto contém endereços `stun:`, `turn:` e/ou `turns:`, além de `username` e `credential` para TURN. Guarde os dados no ambiente do backend. Neste modo não é necessário usar a API Key geral da conta.

A [documentação oficial](https://www.metered.ca/docs/turn-server-service/quickstart/) informa que uma credencial nova pode levar até dois minutos para começar a funcionar. Cota, validade e condições são as exibidas na sua conta; o CallMed não cria conta/plano nem provisiona o serviço.

## Render

| Variável | Valor do painel Metered |
|---|---|
| `Clinica__Tele__IceUrls__0` | URL STUN, incluindo porta |
| `Clinica__Tele__IceUrls__1` | Primeira URL TURN, incluindo porta e parâmetros |
| `Clinica__Tele__IceUrls__2` e seguintes | Demais URLs TURN/TURNS, uma por índice; preserve opções TCP/TLS fornecidas |
| `Clinica__Tele__TurnUsername` | Campo `username` da credencial TURN |
| `Clinica__Tele__TurnPassword` | Campo `credential` da credencial TURN |
| `Clinica__Tele__TurnSecret` | Vazio para usar Metered; reservado ao modo coturn com segredo compartilhado |

Template de nomes em `infra/render/render-metered.env.example`; preencha os valores reais antes de importar. URLs começam com `stun:`, `turn:` ou `turns:`; não são o endereço HTTPS do dashboard ou da API.

O código usa a mesma credencial para as URLs TURN desta lista. Cadastre URLs pertencentes à credencial que criou. Se usar vários provedores com usuários diferentes, eles precisam de configuração separada que esta revisão não implementa.

## Funcionamento e prioridade

- Com `TurnUsername` e `TurnPassword` preenchidos, são usados os valores do provedor, mesmo se restar um `TurnSecret` antigo. Ainda assim, deixe o segredo vazio para manter a configuração clara.
- Se apenas um dos dois estiver preenchido, a configuração é recusada e a chamada mostra que a clínica precisa concluir a configuração do serviço.
- Sem ambos, `TurnSecret` continua gerando credenciais HMAC temporárias de cinco horas para coturn.
- Se há URL TURN mas nenhuma autenticação, a configuração é recusada. STUN sem TURN continua permitido, mas pode não estabelecer conexão entre redes distintas.
- O segredo compartilhado coturn nunca é enviado ao navegador. A credencial TURN do provedor é entregue somente aos participantes autorizados, porque o navegador precisa dela para autenticar no relay.
- A aplicação não usa SDK de vídeo, salas, SFU nem chave de API geral do Metered. O vídeo e a sinalização continuam no fluxo já implementado do CallMed.

## Atualizar e testar

1. Publique o backend completo desta revisão. Se o repositório ainda mistura arquivos de outras versões, substitua a pasta que contém `CallMed.Web.csproj` pela pasta completa do ZIP, mantendo uma cópia anterior fora do repositório.
2. Preencha as variáveis reais, salve e aplique no Render.
3. Use uma consulta configurada como **Online, por vídeo** dentro da janela permitida. O paciente autoriza e ambos entram com suas contas.
4. Teste um participante no Wi-Fi e o outro em rede móvel. Verifique câmera, microfone e encerramento.
5. Se revogar/expirar a credencial no Metered, atualize o ambiente e entre novamente na chamada. Os dados são lidos ao entrar; uma conexão já aberta não é atualizada automaticamente.

Verificações locais:

```sh
dotnet run --project tests/CallMed.Checks -- --turn
dotnet run --project tests/CallMed.Checks -- --teleconsulta
```

As credenciais de teste são fictícias. Não houve acesso à conta Metered do usuário, criação de credencial real, consumo de franquia ou teste autenticado contra o relay real. A validação entre redes distintas depende do teste de campo após preencher as credenciais.
