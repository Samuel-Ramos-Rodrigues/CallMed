# Áreas de médico, atendente e administrador

O CallMed tem entradas próprias para cada função. Todas usam as contas existentes do ASP.NET Core Identity, com as permissões conferidas no servidor.

| Perfil | Endereço de entrada | Área inicial | Quem cria a conta |
|---|---|---|---|
| Médico | `/acesso/medico` | Agenda e atendimentos do próprio médico | Administrador |
| Atendente / Recepcionista | `/acesso/atendente` | Painel da recepção, agendamentos e solicitações | Administrador |
| Administrador | `/acesso/administrador` | Painel da clínica e gestão dos acessos | Administrador existente ou configuração inicial do projeto |
| Paciente | `/acesso/paciente` | Consultas e exames do próprio paciente | Cadastro público de paciente |

Os endereços acima são caminhos relativos ao site onde o projeto for executado. A página inicial também oferece os três botões da equipe. O endereço antigo `/Identity/Account/Login` continua disponível e abre a entrada de paciente; selecione outra área para entrar como profissional.

## Criar acesso de médico

1. Entre pela área do administrador usando sua conta administrativa existente.
2. Abra **Acessos da equipe** no painel ou no menu.
3. Clique em **Cadastrar médico e criar acesso**.
4. Informe nome, especialidade, CRM e agenda semanal; mantenha o cadastro ativo.
5. Clique em **Continuar para criar acesso**. Na próxima tela, informe e-mail e senha inicial e salve.
6. Forneça as credenciais ao médico. Ele entra em `/acesso/medico`.

Para um médico já cadastrado, use **Criar acesso** na lista de médicos da mesma página. Se já tiver conta, **Gerenciar acesso** permite alterar o e-mail ou definir uma nova senha; senha em branco mantém a atual. Médicos inativos precisam ter o cadastro reativado antes da configuração de credenciais.

## Criar acesso de atendente

1. Em **Acessos da equipe**, clique em **Cadastrar atendente**.
2. Informe nome, e-mail e senha e escolha **Atendente** ou **Recepcionista**.
3. Mantenha o acesso ativo e salve. A conta entra em `/acesso/atendente`.

Escolha **Administrador** somente para quem deverá gerenciar a clínica e as contas da equipe. Contas de atendente não recebem essa permissão. O papel interno `Funcionario` foi mantido para os atendentes e recepcionistas, preservando a compatibilidade com o banco existente.

## Permissões e compatibilidade

- O médico continua acessando somente a própria agenda e os históricos de pacientes vinculados aos seus atendimentos.
- O atendente usa os módulos operacionais já existentes; não cria contas nem gerencia as credenciais da equipe.
- As páginas de gestão de contas exigem o papel `Admin`, inclusive quando alguém tenta abrir seus endereços diretamente.
- Selecionar outro portal não altera o perfil da conta. Uma conta de paciente, por exemplo, é recusada na entrada de médico.
- Contas desativadas são recusadas no login e a sessão aberta é encerrada na próxima requisição.
- Cadastros legados sem vínculo podem ser associados à própria conta; um e-mail não permite assumir um cadastro já vinculado a outra pessoa.
- As senhas seguem a política existente: pelo menos 8 caracteres, uma letra minúscula e um número; as tentativas incorretas continuam sujeitas ao bloqueio do Identity.
- Ao sair, o usuário retorna à entrada do próprio perfil. Login e gestão de acessos foram incluídos nas rotas que o PWA não armazena em cache.

Esta alteração não adiciona tabelas nem exige uma nova migration. Mantenha o banco e as configurações da versão anterior. O projeto atualizado precisa ser executado ou publicado para que as telas apareçam no site.

## Verificação

Foi acrescentada a suíte `PortalAcessoChecks` ao projeto de verificações existente. Ela cobre entradas pelo perfil correto, tentativa de usar outro perfil, CPF de paciente, contas inativas, administrador inicial sem cadastro espelhado, vínculos legados e autorização administrativa. A suíte existente de exames mantém os casos de isolamento entre pacientes e médico.

Para executar com o SDK .NET 8:

```bash
dotnet restore CallMed.sln
dotnet build CallMed.sln --no-restore
dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj
```

As verificações usam banco em memória e HTTP simulado, sem enviar mensagens reais. Nesta entrega passaram a análise de sintaxe dos 14 arquivos C# alterados ou novos, a validação do CSS e as verificações do service worker que impedem armazenar as novas páginas de acesso em cache. A compilação, a suíte .NET e os testes em navegador da aplicação não puderam ser executados porque o ambiente de edição não dispõe do SDK .NET 8.

Antes de publicar, confira em uma base de desenvolvimento:

1. Criação de médico e atendente pelo administrador, seguida do login de cada um em sua área.
2. Médico A visualizando somente sua agenda; alteração de IDs não deve liberar consultas ou exames do médico B.
3. Atendente tentando abrir `/Acessos`, `/MedicoAcesso/Gerenciar/ID` ou `/Funcionario/Create`: deve ter acesso negado.
4. Conta de paciente tentando entrar como médico, e atendente tentando entrar como administrador: devem ser recusados.
5. Alteração da senha de um médico pela administração: a nova senha funciona e a antiga deixa de funcionar.
6. Desativação de conta: um novo login é recusado e uma sessão aberta é encerrada na próxima requisição.
7. Saída do sistema e retorno à tela do perfil correto, inclusive no aplicativo instalado.
