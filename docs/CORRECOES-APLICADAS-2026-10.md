# Correções aplicadas — outubro de 2026

Esta versão aplica correções pontuais decorrentes do parecer técnico. Não substitui homologação nem auditoria de segurança.

## 1. Webhooks de atendimento

- `WhatsAppEvolution`, `Sms` e `Email` exigem o segredo via cabeçalho HTTP.
- Cabeçalhos aceitos: `X-CallMed-Webhook-Secret` (preferencial) e `X-MKSAN-Webhook-Secret` (legado).
- O segredo em query string não é mais aceito para evitar exposição em logs, proxies, histórico e telemetria.
- Os corpos das requisições estão limitados a 1 MiB por endpoint.
- Testes automatizados foram adicionados em `tests/CallMed.Checks/Program.cs`.

**Ação de implantação:** reconfigure qualquer fornecedor que ainda transmita segredo em URL para usar um dos cabeçalhos acima. O endpoint retornará `401 Unauthorized` para esse método antigo.

## 2. Proxies confiáveis

A aplicação não limpa mais automaticamente `KnownNetworks` e `KnownProxies`. Isso evita confiar em `X-Forwarded-For` encaminhado por qualquer origem. Para hospedagem atrás de proxy reverso, liste somente os IPs reais dos proxies:

```dotenv
ReverseProxy__KnownProxies__0=10.0.0.10
```

O endereço acima é somente exemplo; substitua pelo IP real da sua infraestrutura. Se houver mais de um proxy, inclua outros índices. Não inclua IPs de usuários/clientes. Após configuração, valide que o endereço do cliente e o HTTPS sejam detectados corretamente pela aplicação.


## 3. Centralização do acesso

- O site público e os links de retorno do cadastro/recuperação apresentam um único caminho de entrada (`/acesso`); removi os cartões públicos separados de médico, atendente e administrador.
- A página de login não solicita mais que a pessoa selecione um perfil. O formulário aceita CPF de paciente ou e-mail da conta; o papel e o estado do cadastro são verificados no backend antes da autenticação.
- Todas as URLs de acesso geradas pela aplicação apontam para `/acesso`. URLs legadas (`/acesso/{perfil}`) e o caminho padrão do Razor Pages redirecionam para `/acesso`; o segmento recebido não escolhe nem concede permissões.
- A mensagem para identificador/senha inválidos é genérica para não confirmar se uma conta existe. Após autenticar, o usuário é encaminhado à área correspondente ao papel registrado.
- A remoção dos links por perfil melhora a clareza e reduz exposição desnecessária na interface, mas não substitui autorização nas rotas e nos controladores.

## 4. Próxima homologação obrigatória

Na máquina de desenvolvimento com .NET 8 SDK:

```bash
dotnet restore CallMed.sln
dotnet build CallMed.sln --configuration Release
dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj
```

Para testes de interface:

```bash
cd tests
npm install
npm run test:dom
npm run test:browser
```

Não foram executados testes integrados com PostgreSQL, Evolution, SMTP, SMS ou HIS neste ambiente. Antes da produção, teste reservas simultâneas no mesmo horário, tentativas idempotentes, autenticação dos webhooks, permissões por paciente/perfil, sincronização offline, backups/restauração e entrega de lembretes, usando dados fictícios e ambiente controlado.
