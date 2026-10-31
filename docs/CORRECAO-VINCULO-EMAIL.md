# Correção: identificação automática em conversas de e-mail existentes

Data: 31/10/2026

## Problema

O método `AtendimentoConversaService.ObterOuCriarAsync` respeitava
`resolverIdentidade: false` na criação de conversas, mas ignorava esse parâmetro
quando uma conversa existente não possuía `PacienteId`. Assim, a entrada de uma
nova mensagem de um remetente cujo e-mail constava no cadastro poderia associar
a conversa a um paciente sem verificação de identidade.

## Alteração

- A resolução automática agora só é executada para conversas **novas e existentes**
  se `resolverIdentidade` for `true`.
- Um `pacienteId` explicitamente informado continua podendo associar um paciente
  ativo à conversa, mesmo com identificação automática desabilitada.
- Vínculos já existentes são preservados; a alteração não modifica registros
  antigos nem exige migração do PostgreSQL.
- O fluxo do leitor Gmail segue chamando `ObterOuCriarAsync` com
  `resolverIdentidade: false`, e a IA recebe contexto anônimo na entrada de e-mail.

## Regressões adicionadas

Arquivo: `tests/CallMed.Checks/AtendimentoIdentidadeChecks.cs`.

1. Conversa de e-mail existente permanece sem vínculo quando `resolverIdentidade: false`.
2. Conversa nova permanece sem vínculo quando `resolverIdentidade: false`.
3. Identificação automática continua disponível com `resolverIdentidade: true`.
4. Vínculo explícito com `pacienteId` continua permitido.
5. Vínculo já confirmado não é apagado automaticamente.

## Como verificar antes de publicar

No diretório raiz, com SDK .NET 8 instalado:

```sh
dotnet restore CallMed.sln
dotnet build CallMed.sln -c Release --no-restore
dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj -c Release
```

Para testar o Dockerfile usado pelo Render:

```sh
docker build -t callmed:validacao .
```

## Situação da validação nesta entrega

- Código inspecionado e condição corrigida: **sim**.
- Sintaxe dos arquivos JavaScript verificada com `node --check`: **sim**.
- Integridade e consistência de arquivos do ZIP: **sim**.
- Compilação .NET, testes de integração com banco, Gmail e Render: **não executados**
  neste ambiente, que não possui o SDK .NET 8 nem credenciais de integração.

Realize as verificações acima antes de publicar. Em produção, valide uma conversa
existente sem vínculo cujo remetente também aparece no cadastro de pacientes;
confirme que o e-mail foi registrado sem associar `PacienteId` automaticamente.
