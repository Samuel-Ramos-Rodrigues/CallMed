# Minha Conta e avaliação no encerramento

Atualização de 05/11/2026. Projeto completo, baseado na versão que já contém as correções de redefinição de senha, tempo de resposta do assistente, avaliações e TWA.

## Minha Conta

A rota `/MinhaConta` agora atende paciente, médico, atendente e administrador, sempre com os dados da própria conta. As três abas mantêm o visual e os temas existentes:

- **Meus dados:** foto JPG/PNG de até 2 MB, nome, e-mail e telefone. O paciente também pode atualizar nascimento e convênio. CPF, CRM, especialidade, cargo e papel não podem ser modificados pelo formulário de perfil.
- **Segurança:** troca de senha com confirmação da senha atual, dispositivos/acessos e atividade recente. A troca de senha encerra os outros acessos e preserva o atual. É possível revogar um acesso específico ou todos os outros. Logins antigos sem identificação de dispositivo são encerrados pela alteração do security stamp.
- **Preferências:** tema e acessibilidade salvos na conta e aplicados no próximo login. O paciente escolhe quais canais podem receber lembretes de consulta e avisos da lista de espera. A preferência não muda o provedor nem impede respostas em uma conversa iniciada pelo paciente.

Alterar o e-mail exige a senha atual e verifica duplicidade; Identity e cadastro clínico são atualizados juntos. Foto e sessões são consultadas pelo titular autenticado, sem aceitar ID de outro usuário. Fotos ficam em tabela separada para não carregar os bytes durante as verificações comuns da conta. A atividade guarda descrições do navegador/sistema e ações da conta, sem IP ou User-Agent bruto.

Sem canais digitais autorizados/disponíveis, os serviços existentes podem gerar uma tarefa de ligação para a recepção. Escolher “Ligação da recepção” mantém o fluxo manual anterior. Registros sem preferências mantêm os canais atuais.

A remoção de acesso do paciente continua disponível, recolhida na aba Segurança. Agora exige senha e confirmação explícita; o histórico clínico permanece com a clínica.

## Despedida e feedback

O prompt permite que o agente use `encerrar_atendimento` quando identificar uma despedida. O backend valida a intenção e controla a pesquisa. “Obrigado” sozinho, uma nova pergunta, pedido de agendamento ou confirmação pendente não autorizam o encerramento por inferência.

Exemplos:

- “Por agora estou satisfeito, nos falamos depois”: o agente pode sinalizar encerramento.
- “Não preciso de mais nada, obrigado”: pode sinalizar encerramento.
- “Obrigado”: a conversa continua.
- “Não quero mais remarcar”: continua, sem confundir a decisão sobre a consulta com despedida.
- “Até depois, mas qual é o endereço?”: responde à pergunta.

A detecção pelo modelo não é determinística: se ele não sinalizar uma despedida, continuam disponíveis o botão **Encerrar e avaliar** do site, os comandos explícitos e o encerramento pela equipe. A IA não encerra automaticamente só porque já respondeu a uma pergunta, nem por tempo de inatividade.

No site, o encerramento disponibiliza o cartão com notas de 1 a 5 e comentário opcional. Nos canais externos, o convite é enviado pela fila existente, uma única vez por ciclo. Não há a etapa “quer receber o formulário?”. O paciente pode avaliar diretamente ou ignorar. SMS usa uma mensagem curta.

Uma entrada nova ou tomada humana durante a geração impede o encerramento obsoleto. Gerar um rascunho de e-mail manualmente continua sem envio automático; o encerramento inferido no fluxo de e-mail exige o modo automático.

As respostas da avaliação ficam separadas do contexto da IA, não fazem agendamentos e aparecem para a equipe em `/Avaliacoes`.

## Publicação no Render

1. Atualize o código com o conteúdo deste projeto completo e publique novamente.
2. Mantenha as variáveis atuais. Esta entrega não exige novas API Keys ou segredos.
3. A inicialização aplica o patch aditivo **V30**, criando `ContaPerfis`, `ContaFotos`, `ContaSessoes` e `ContaEventos`. Ele não recria o banco nem remove dados existentes. A opção `Database__ApplyV30Patch` tem padrão `true`.
4. Se sua instalação desativar patches automáticos, execute `docs/sql/V30-ContaPerfil.sql` antes de iniciar a nova versão.
5. Faça um novo login para que o dispositivo apareça na aba Segurança. Preferências antigas do navegador continuam funcionando; ao salvar/login, as escolhas da conta são aplicadas.
6. Confira `/MinhaConta`, a troca de senha e um encerramento com avaliação. O cache do PWA foi atualizado para a nova versão.

As chaves persistidas do Data Protection e o patch V29 são preservados. Para testar recuperação de senha, solicite um link novo; não reutilize links já consumidos.

## Validação local

```bash
dotnet run --project tests/CallMed.Checks
# Apenas os novos fluxos de conta/encerramento:
CALLMED_CONTA_FIXTURES=/tmp/callmed-conta dotnet run --project tests/CallMed.Checks -- --conta
# Usando Playwright/Chromium instalados:
CALLMED_CONTA_FIXTURES=/tmp/callmed-conta NODE_PATH=<node_modules> CHROME_PATH=<chromium> node tests/minha-conta-browser.cjs
NODE_PATH=<node_modules> CHROME_PATH=<chromium> node tests/feedback-browser.cjs
```

Os testes HTTP usam MVC, Razor, Identity e cookies reais sobre um banco em memória. Os testes do agente usam respostas de modelo e provedores simulados; não enviam mensagens reais. A verificação do navegador usa o HTML produzido pelos testes HTTP e o CSS/JS reais. A homologação no ambiente Render, no PostgreSQL e nos aparelhos/provedores da clínica deve ser feita após a publicação.
