# Redefinição de senha — correção de 04/11/2026

## Comportamento corrigido

O teste HTTP reproduziu uma falha na validação dos dois formulários da mesma página: ao enviar somente a nova senha e os dados do link, o model binder também exigia o CPF/e-mail do formulário de solicitação. O erro podia ficar na chave simples `Usuario`, enquanto o handler removia apenas `Input` e `Input.Usuario`. Isso impedia chegar à gravação da senha; a chave simples agora é tratada sem remover os erros da nova senha.

Havia também um segundo caso: a redefinição alterava o hash pelo Identity, mas não removia um bloqueio temporário por tentativas incorretas. Esse bloqueio foi reproduzido e corrigido.

A correção:

- Valida o token e a conta antes de apresentar o formulário do link.
- Mantém o vínculo entre token, UID e e-mail, a expiração e o uso único. Links legados continuam aceitos enquanto forem válidos.
- Mantém a política da senha e exibe os erros, inclusive os associados a campos ocultos do link.
- Remove o bloqueio temporário e zera tentativas após uma redefinição válida. Bloqueios administrativos do cadastro e contas desativadas não são reativados pela recuperação.
- Recarrega a conta do armazenamento e verifica a nova senha antes de confirmar. No PostgreSQL, redefinição e desbloqueio temporário ficam na mesma transação.
- Mostra uma página explícita de confirmação e o botão **Entrar com a nova senha**. Uma falha de gravação não apresenta sucesso.
- Guarda as chaves do Data Protection no banco, com nome estável da aplicação, para validar links após reiniciar o contêiner.

As falhas foram reproduzidas em testes locais. Não foram acessados os logs nem a conta de produção do usuário.

## Como atualizar no Render

1. Publique o projeto completo atualizado no serviço CallMed.
2. A inicialização aplica o patch aditivo **V29**, que cria `DataProtectionKeys`. Não modifica nem recria contas, pacientes ou hashes de senha existentes.
3. Use **Esqueci minha senha** para solicitar um **novo link após esta atualização**. A mudança do armazenamento das chaves pode invalidar links e sessões anteriores uma vez. As próximas chaves ficam persistidas no banco.
4. Abra o novo e-mail, digite e confirme a nova senha, aguarde **Senha alterada com sucesso** e use o botão para entrar.

Não há nova variável obrigatória. É usada a conexão PostgreSQL que o projeto já possui. `Database:ApplyV29Patch` é `true` por padrão. O SQL está também em `docs/sql/V29-DataProtectionKeys.sql`, para instalações que executam patches manualmente.

As chaves são material interno de autenticação. Mantenha o acesso à tabela restrito à aplicação e à administração do banco, como as configurações de acesso. Não apague suas linhas durante limpeza de histórico. O armazenamento segue o provedor oficial `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 8.0.20.

## Confirmação dos testes

A suíte usa o Identity real, armazenamento EF de teste e e-mail simulado. O teste HTTP inicia um servidor local com as páginas Razor reais e executa:

`Esqueci minha senha → e-mail capturado → link → formulário com antiforgery → nova senha → confirmação → login → cookie autenticado`.

Também verifica senha antiga recusada, login por CPF na mesma conta, bloqueio temporário, cadastro inativo, token consumido/expirado/adulterado, política de senha, falha simulada de persistência e validade do link entre dois provedores independentes compartilhando o mesmo banco de teste.

```sh
dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj
dotnet publish CallMedCrud/CallMedCrud.csproj --configuration Release
```

O teste não envia e-mails reais nem altera contas de produção. A homologação no Render usa um novo link emitido pela versão publicada.

## Referência técnica

[Microsoft: persistência de chaves do Data Protection com Entity Framework Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-8.0#persist-keys-in-a-database-persistkeystodbcontext).
