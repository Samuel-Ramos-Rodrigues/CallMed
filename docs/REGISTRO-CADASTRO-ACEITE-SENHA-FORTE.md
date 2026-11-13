# Cadastro: aceite legal obrigatório e senha forte

## Aceite de Termos e Privacidade

- A etapa 3 do cadastro de paciente exibe uma caixa desmarcada para o usuário confirmar que leu e aceita os Termos de Serviço e a Política de Privacidade.
- O navegador bloqueia o envio quando a caixa não está marcada e o servidor verifica a mesma condição independentemente do JavaScript.
- O aceite é armazenado em claims do ASP.NET Identity, sem exigir alteração de schema: versão dos Termos (`callmed/terms-accepted-version`), versão da Política (`callmed/privacy-accepted-version`) e timestamp UTC (`callmed/legal-consent-accepted-at-utc`).
- Versões atuais: Termos `2026-10-29` e Política de Privacidade `2026-10-29`. Atualizar as constantes em `Register.cshtml.cs` sempre que os textos legais forem alterados.

## Requisitos de senha

O `UserManager` exige:

1. Pelo menos 8 caracteres.
2. Pelo menos uma letra maiúscula.
3. Pelo menos uma letra minúscula.
4. Pelo menos um número.
5. Pelo menos um sinal de pontuação ou símbolo, como `!`, `#` ou `@`. Espaços em branco não atendem a esse requisito.

O requisito de símbolo é aplicado no servidor pelo `SpecialCharacterPasswordValidator`, além das opções padrão do ASP.NET Identity. Isso vale para cadastro, recuperação de senha e criação/alteração de senha por `UserManager`. A tela de cadastro também apresenta validação visual ao digitar, mas o servidor é a autoridade final.

## Testes manuais recomendados

- Tentar criar conta sem marcar a caixa: cadastro deve ser bloqueado no cliente e no servidor.
- Marcar a caixa e usar `senha123`: deve ser rejeitada (sem maiúscula e sem símbolo).
- Usar `SenhaSegura1`: deve ser rejeitada (sem símbolo).
- Usar `Senha segura1!`: deve ser aceita pela política de senha (desde que as demais validações de cadastro estejam corretas).
- Repetir o teste no fluxo de recuperação de senha e na criação de senha inicial para médico/funcionário.
