# Termos de Serviço e URLs para Google OAuth

## Rota pública adicionada

A página de Termos de Serviço está em `CallMedCrud/Views/Home/TermosDeUso.cshtml`, exposta anonimamente em `GET /termos-de-uso`. Ela usa o layout público e não exige login.

Depois que a versão for publicada no Render, o endereço esperado será:

`https://callmed.onrender.com/termos-de-uso`

O endereço de privacidade existente permanece:

`https://callmed.onrender.com/privacidade`

Os links de ambas as páginas foram adicionados ao rodapé público e de autenticação, e os rodapés da área autenticada também apontam para os documentos.

## Antes de enviar ao Google

- Abra os dois endereços em janela anônima e confirme que respondem sem autenticação.
- Confirme que a URL da Política de Privacidade no Google Auth Platform é idêntica à página pública e que a homepage inclui um link visível.
- Verifique as configurações institucionais: `PlayStore:SupportEmail` (preferencial) ou `Clinica:Email`. O fallback `callmed.atendimento@gmail.com` é usado somente se nenhum desses valores estiver configurado.
- A política de privacidade foi atualizada para explicar a leitura da caixa de Gmail dedicada, os dados usados no processamento, a etiqueta de duplicidade e o uso restrito de IA para gerar rascunhos administrativos que exigem aprovação humana. Revise o texto para garantir que corresponda exatamente à configuração ativa em produção.
- O Google pode exigir que homepage e documentos legais estejam em um domínio verificado que você controla. `callmed.onrender.com` pode servir para testar publicamente a rota, mas não é um domínio próprio para verificação da marca. Para a submissão de produção, considere configurar um domínio próprio e verificá-lo no Google Search Console/Google Auth Platform conforme solicitado.

## Conteúdo legal

O texto é uma base operacional para o serviço CallMed e precisa ser revisado pela organização responsável/assessoria jurídica antes de publicação definitiva, incluindo identificação da entidade operadora, contatos, retenção e práticas reais. Não deixe os dados de contato vazios no ambiente de produção.


## Aceite obrigatório no cadastro e política de senha (29/10/2026)

- O cadastro de paciente exibe uma caixa de seleção desmarcada para concordar com os Termos de Serviço e a Política de Privacidade. O servidor rejeita o cadastro se a caixa não estiver marcada.
- A evidência do aceite é guardada como claims da conta Identity: versão dos Termos, versão da Política de Privacidade e horário UTC do aceite. Isso utiliza a tabela existente de claims do Identity e não exige coluna nova no banco.
- A política de senha do ASP.NET Identity exige pelo menos 8 caracteres, uma letra maiúscula, uma letra minúscula, um número e um sinal de pontuação/símbolo (por exemplo `!`, `#` ou `@`); espaços em branco não contam como caractere especial. A regra vale para cadastro, redefinição e criação/alteração de credenciais por `UserManager`, incluindo o validador `SpecialCharacterPasswordValidator`. Se o bootstrap de administrador for habilitado, a senha configurada também precisa cumprir essa política.
- O cadastro apresenta validação visual no navegador, mas a regra definitiva permanece aplicada no servidor.
- A versão identificada como aceita é `2026-10-29`; a versão deve ser atualizada no modelo de cadastro quando os documentos legais forem alterados.
