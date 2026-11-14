# Android nativo — integração com CallMed

O cliente está em [`mobile/android`](../../mobile/android/README-ANDROID.md). O APK nativo **24.0.0 / 2400** está compilado e incluído no download do site. **Publique primeiro o backend deste ZIP e depois instale o APK** sobre a versão anterior. A jornada exige o patch V31; veja [instalação e configuração](../ARQUITETURA.md). O formato de chat por atendimento da versão 23.2.0 foi preservado.

Preparação, confirmação ativa, check-in e ofertas de encaixe são nativos. Formulários cifrados e vídeo usam o navegador por ticket temporário de uso único e sessão restrita à consulta. O token de acesso da API não é transmitido ao navegador. Não há sincronização geral dos cookies do site com o app.

## API móvel

Prefixo `/api/mobile/v1`. Login e refresh retornam os campos padrão `tokenType`, `accessToken`, `expiresIn`, `refreshToken`. As demais rotas exigem `Authorization: Bearer …`. O cookie do site continua sendo o padrão do site e não autentica essas rotas.

| Rota | Finalidade |
|---|---|
| `GET /config` | Versão da API e perfil disponível, sem autenticação |
| `POST /auth/login` | CPF/e-mail e senha de paciente; sessão própria por dispositivo |
| `POST /auth/refresh` | Renovação com sessão, papel, conta e security stamp conferidos |
| `POST /auth/logout` | Revogação da sessão atual |
| `GET /perfil` · `PUT /perfil` | Dados do titular; CPF/e-mail/papel não são alterados pelo binding |
| `PUT /perfil/preferencias` | Atualização parcial de tema, avisos e acessibilidade; campos omitidos são preservados |
| `PUT /perfil/email` | Senha atual, novo e-mail único e confirmação; sincroniza Identity/cadastro clínico e retorna novos tokens |
| `POST /perfil/senha` | Identity ChangePassword; retorna tokens novos para a sessão atual e revoga outros acessos |
| `GET /perfil/sessoes` | Acessos ativos do titular, com indicação da sessão atual |
| `POST /perfil/sessoes/{id}/encerrar` | Encerra outro acesso do titular, com confirmação explícita |
| `POST /perfil/sessoes/encerrar-outras` | Encerra os demais acessos e mantém a sessão atual, com confirmação |
| `GET /perfil/foto` · `POST /perfil/foto` | Foto do titular, com os validadores existentes |
| `GET /medicos` | Médicos ativos e especialidades |
| `GET /medicos/{id}/datas` | Datas realmente disponíveis, com o mesmo horizonte cadastrado no site |
| `GET /medicos/{id}/elegibilidade` | Elegibilidade do próprio paciente, conforme matriz da clínica |
| `GET /medicos/{id}/horarios?data=AAAA-MM-DD` | Vagas; `consultaId` opcional apenas para remarcação própria |
| `GET /agenda/primeira-vaga?especialidade=...` | Procura primeira vaga da especialidade em até 90 dias, como o site |
| `GET /consultas` · `POST /consultas` | Lista própria e agendamento com confirmação explícita |
| `POST /consultas/{id}/confirmar` | Confirmação de consulta própria |
| `POST /consultas/{id}/remarcar` | Nova data/hora para consulta própria |
| `POST /consultas/{id}/cancelar` | Cancelamento de consulta própria |
| `GET /exames` | Até 200 registros ativos/aprovados do próprio paciente, do mais recente ao mais antigo |
| `GET /atendimento/historico` | Só o atendimento atual; até 60 mensagens por página, ciclo e indicação de arquivos anteriores |
| `GET /atendimento/anteriores` · `GET /atendimento/anteriores/{id}` | Lista paginada de atendimentos finalizados e consulta das mensagens do titular |
| `POST /atendimento/iniciar` | Arquiva a conversa ativa com confirmação e abre atendimento vazio |
| `POST /atendimento/mensagens` | Mesmo fluxo do agente, confirmação de ações e pausa humana |
| `POST /atendimento/encerrar` · `POST /atendimento/avaliar` | Mesmo encerramento e avaliação do site |

A API reutiliza `AgendamentoService` e o fluxo existente do agente. A seleção de paciente nunca vem do corpo da requisição. Data Protection, sessões, perfis e registros de atividade usam as tabelas já existentes. Não há nova chave JWT nem alteração nas credenciais de WhatsApp, SMS e e-mail.

Agendar cria ou reutiliza a solicitação confirmada vinculada à consulta; cancelar também atualiza a solicitação. As ações registram auditoria e uma repetição da mesma reserva não duplica a solicitação. As permissões são as mesmas dos serviços do site. O campo `futura` separa a classificação do histórico das permissões de ação por situação.

## Leitura e continuidade de sessão

`GET /perfil` retorna `contaId` e os campos `textoMaior`, `altoContraste`, `reduzirAnimacoes` e `modoSimplificado`. O app grava apenas preferências visuais no armazenamento privado, por conta. Elas podem ser alteradas sem internet e sincronizadas depois. Os níveis normal/grande/extra são locais; a preferência booleana de texto maior é compartilhada com o site. A escala efetiva nunca reduz a fonte escolhida no Android.

O painel Leitura mantém a tela de origem composta, sem interação e fora da árvore de acessibilidade enquanto o painel está aberto. Assim os rascunhos permanecem em memória, sem gravar senhas em disco. Títulos, papéis, estados e descrições auxiliam a navegação com leitor de tela; a ordem real de foco ainda deve ser homologada com TalkBack no aparelho.

Os campos do `PUT /perfil/preferencias` são opcionais: clientes anteriores podem enviar só suas opções sem apagar acessibilidade. Tema e canal, quando presentes, precisam ser válidos. E-mail e senha devolvem o mesmo formato de tokens do login, sem cookie; o app substitui o par antigo e mantém o identificador da sessão atual. As outras sessões são revogadas. A geração local de sessão impede respostas atrasadas de apagar os tokens recém-renovados.

Os exames usam os campos existentes no site: nome, data e local. Não há arquivos PDF ou laudos armazenados por esse cadastro. O formulário de avaliação do chat só aparece no app depois do aceite do convite; a API de atendimento existente continua atendendo os demais canais.

## Histórico e retomada do chat

Uma nova pergunta reabre a conversa no servidor, inclusive quando a avaliação anterior ainda está pendente. A API não exige avaliar antes de continuar. No app, cada ação invalida as leituras de histórico iniciadas antes dela. Só há um polling em andamento; uma resposta antiga não pode recolocar o estado encerrado nem apagar mensagens novas. O resultado do POST aplica seu estado antes da atualização do histórico. Falhas de rede não repetem automaticamente o envio.

O formato `historicoVersao=2` separa cada atendimento pelos encerramentos já persistidos. A conversa finalizada sai da tela atual e permanece em Anteriores, em modo de leitura. Mensagens Web/app novas não apagam arquivos ao superar o limite antigo de 250. Detalhes, compatibilidade e paginação em [CHAT-POR-ATENDIMENTO.md](../funcionalidades/CHAT-POR-ATENDIMENTO.md).

O aviso antigo de encerramento e seu convite de avaliação não aparecem durante o preparo da resposta à nova mensagem. O campo de texto e o botão de envio ficam lado a lado, com descrição acessível e alvo de toque de 56dp.

## Verificação local

```sh
dotnet run --project tests/CallMed.Checks -- --mobile
```

Execute na raiz do projeto, contendo `src/CallMed.Web`. A suíte sobe HTTP local com Identity e banco em memória. IA simulada; nenhuma mensagem externa é enviada. A suíte completa também inclui as verificações de login web, recuperação de senha, perfil, feedback e canais anteriores.

Antes de uso com pacientes, homologue no aparelho: login, agendamento, mudança de tela, foto, teclado, rotação, atendimento humano, avaliação e reinstalação por cima do APK anterior. Os testes automatizados e a compilação não substituem essa verificação em um celular físico.
