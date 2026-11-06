# Android nativo — integração com CallMed

O novo cliente está em [`CallMedAndroid`](../CallMedAndroid/README-ANDROID.md). Publique primeiro o backend desta entrega; depois instale o APK nativo. O pacote do site passa a ser a versão **23.0.0 / 2300** do app de pacientes.

## API móvel

Prefixo `/api/mobile/v1`. Login e refresh retornam os campos padrão `tokenType`, `accessToken`, `expiresIn`, `refreshToken`. As demais rotas exigem `Authorization: Bearer …`. O cookie do site continua sendo o padrão do site e não autentica essas rotas.

| Rota | Finalidade |
|---|---|
| `GET /config` | Versão da API e perfil disponível, sem autenticação |
| `POST /auth/login` | CPF/e-mail e senha de paciente; sessão própria por dispositivo |
| `POST /auth/refresh` | Renovação com sessão, papel, conta e security stamp conferidos |
| `POST /auth/logout` | Revogação da sessão atual |
| `GET /perfil` · `PUT /perfil` | Dados do titular; CPF/e-mail/papel não são alterados pelo binding |
| `PUT /perfil/preferencias` | Tema e avisos existentes; preserva acessibilidade configurada no site |
| `POST /perfil/senha` | Identity ChangePassword, revogação de acessos e novo login |
| `GET /perfil/foto` · `POST /perfil/foto` | Foto do titular, com os validadores existentes |
| `GET /medicos` | Médicos ativos e especialidades |
| `GET /medicos/{id}/datas` | Datas disponíveis, no horizonte de 90 dias |
| `GET /medicos/{id}/horarios?data=AAAA-MM-DD` | Vagas; `consultaId` opcional apenas para remarcação própria |
| `GET /consultas` · `POST /consultas` | Lista própria e agendamento com confirmação explícita |
| `POST /consultas/{id}/confirmar` | Confirmação de consulta própria |
| `POST /consultas/{id}/remarcar` | Nova data/hora para consulta própria |
| `POST /consultas/{id}/cancelar` | Cancelamento de consulta própria |
| `GET /atendimento/historico` | Conversa Web canônica da conta, últimas 60 mensagens |
| `POST /atendimento/mensagens` | Mesmo fluxo do agente, confirmação de ações e pausa humana |
| `POST /atendimento/encerrar` · `POST /atendimento/avaliar` | Mesmo encerramento e avaliação do site |

A API reutiliza `AgendamentoService` e o fluxo existente do agente. A seleção de paciente nunca vem do corpo da requisição. Data Protection, sessões, perfis e registros de atividade usam as tabelas já existentes. Não há nova chave JWT nem alteração nas credenciais de WhatsApp, SMS e e-mail.

## Verificação local

```sh
dotnet run --project tests/CallMed.Checks -- --mobile
```

Execute na raiz do projeto, contendo `CallMedCrud`. A suíte sobe HTTP local com Identity e banco em memória. IA simulada; nenhuma mensagem externa é enviada. A suíte completa também inclui as verificações de login web, recuperação de senha, perfil, feedback e canais anteriores.

Antes de uso com pacientes, homologue no aparelho: login, agendamento, mudança de tela, foto, teclado, rotação, atendimento humano, avaliação e reinstalação por cima do APK anterior. Os testes automatizados e a compilação não substituem essa verificação em um celular físico.
