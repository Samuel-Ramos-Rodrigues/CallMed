# CallMed V22 — inclusão e continuidade do atendimento

Esta atualização parte da V21.6.5 enviada e mantém o MVC, Identity, PostgreSQL/Neon, agente e canais existentes.

## O que mudou

- Cadastro administrativo de paciente com e-mail opcional, inclusive pelo assistente usado pela equipe. E-mail preenchido continua sendo validado. Paciente com conta digital vinculada mantém e-mail para login. O banco admite vários pacientes sem e-mail sem remover a unicidade dos e-mails informados.
- Cadastro presencial sem e-mail não pode ser apropriado pelo formulário público apenas com o CPF. A recepção deve verificar a identidade e cadastrar o e-mail antes da criação do acesso.
- Fila de contingência da recepção em `/contingencia.html`: contatos e preferências salvos no navegador com AES-GCM, chave derivada da senha por PBKDF2, bloqueio manual e após cinco minutos sem atividade. Nenhuma senha é salva.
- Reconciliação em **Solicitações → Pedidos registrados sem internet**: funcionário/admin revisa cada pedido. POST protegido por autenticação e antiforgery. Protocolo UUID, bloqueio transacional no PostgreSQL e índice único evitam duplicidade nos reenvios. Só há remoção local após confirmação do servidor.
- O horário original da captura compõe `CriadoEm`, para não esconder a espera durante a queda. `RecebidaNoServidorEm` registra a sincronização. O paciente e a especialidade definitivos são conferidos na triagem.
- Orientações administrativas comuns na tela de detalhes/comprovante, retorno da ferramenta de agendamento do agente e lembretes. Após marcar ou remarcar pelo site, abre o detalhe da consulta.
- Comprovante imprimível com os dados da consulta. Documentos, antecedência e orientações por especialidade podem ser configurados no servidor.
- Histórico administrativo de exames realizados: nome, data e local. Equipe registra/desativa; paciente vê somente o próprio histórico; médico vê pacientes com consulta não cancelada vinculada a ele. A desativação preserva registro e auditoria. Não há upload de laudos ou interpretação de resultados.
- Administrador pode consultar a conexão real da instância em **Integrações → Verificar conexão com a Evolution**. A consulta não envia mensagem. Webhook aceita `X-CallMed-Webhook-Secret`, preservando o cabeçalho antigo.
- Exemplo de implantação conjunta em Docker Compose e script PowerShell para conectar a instância. Não foi implantado em servidor externo.

## Atualizar o banco

O projeto original já usa migrations para a base e patches idempotentes V12–V21 no startup. A V22 segue esse fluxo: `Database:ApplyV22Patch=true` (padrão) executa após os patches anteriores.

O patch V22:

1. Permite `NULL` em `Pacientes.Email` e normaliza e-mails vazios de cadastros sem conta.
2. Adiciona chave única da contingência e data de recebimento no servidor.
3. Cria `ExamesHistorico` e o índice por paciente/data.

Use uma cópia de homologação e backup do banco antes de atualizar a produção. Não execute uma nova migration gerada automaticamente sem revisar: a base enviada já contém tabelas mantidas por patches fora do snapshot de migrations. Para banco novo, aplique as migrations existentes (`Database__AutoMigrate=true`) e depois os patches do startup. Em banco existente, preserve a política atual de migrations. O usuário do banco precisa executar ALTER/CREATE para os patches; se a empresa utiliza implantação administrada, o DBA aplica o SQL revisado do initializer e só então desabilita `ApplyV22Patch`.

## Configurações de orientações

Exemplos em variáveis do servidor:

```dotenv
Clinica__Endereco=Endereço completo da unidade
Clinica__Telefone=Telefone da recepção
Clinica__Orientacoes__AntecedenciaMinutos=15
Clinica__Orientacoes__Documentos=Leve um documento de identificação e seu CPF.
Clinica__Orientacoes__PorEspecialidade__3=Orientação administrativa aprovada para a especialidade 3.
```

O número `3` é o ID real da especialidade. A clínica precisa aprovar o conteúdo antes de usar. O sistema não inventa jejum, suspensão de medicamentos nem preparo clínico. As orientações são consultadas da configuração atual; não são um histórico imutável das orientações antigas.

## Limites operacionais

- O dispositivo precisa ter aberto o CallMed online e instalado/atualizado seu service worker antes da queda. O formulário exige HTTPS (ou localhost), Web Crypto e Web Locks. Um primeiro acesso sem internet não baixa o aplicativo.
- A fila pertence ao navegador/dispositivo, não à conta. Use equipamento da clínica; limpar os dados do navegador ou perder a senha elimina a possibilidade de recuperação. Não guarda agenda, senha de login, CPF ou laudos no cache.
- Internet voltou: abrir a página autenticada de reconciliação no mesmo navegador, desbloquear a fila e enviar os pedidos individualmente. Se a sessão expirar, entrar novamente; reenvio conserva o protocolo.
- Sem navegador compatível: usar a ficha de contingência descrita em `ROTEIRO-HOMOLOGACAO-V22.md`, guardar sob responsabilidade da recepção e digitar depois. Nenhum horário é prometido antes da consulta da agenda central.
- Mensagens, SMS, SMTP, agente e API legada ainda dependem das configurações e credenciais do ambiente. Uma instância Evolution conectada não comprova recebimento e resposta pelo webhook.
- Não há resultado real de redução de faltas ou de custos até a execução do piloto com a clínica. O plano de medição está no roteiro.

## Testar

```bash
dotnet build CallMed.sln -c Release
dotnet run --project tests/CallMed.Checks -c Release
# Testes da fila com DOM simulado:
npm install --prefix tests
npm --prefix tests run test:dom
# Teste visual/funcional em navegador real, depois de instalar Chromium:
npx --prefix tests playwright install chromium
npm --prefix tests run test:browser
```

Consulte `validacao/VALIDACAO-V22.md` para distinguir o que foi executado nesta entrega do que depende do seu ambiente.
