# CallMed V28 — clínica multiprofissional e navegação organizada

A demanda descreve consultórios e clínicas em geral. A empresa de Psicologia é um contexto de aplicação, não uma limitação do produto a uma única profissão. A V28 mantém um núcleo compartilhado de agendamento, recepção, comunicação, fila de espera, preparação, consulta online e indicadores. Psicologia permanece como módulo específico de registros e guarda técnica.

## Como ficou organizado

| Perfil | Menu principal | Outras funções |
| --- | --- | --- |
| Recepção / administração | Início, Agenda, Recepção, Mensagens | Menu → Áreas do sistema |
| Profissional de saúde | Minha agenda, Atendimentos, Minha conta | Menu → Áreas do sistema |
| Paciente | Início, Consultas, Agendar, Conta | Menu → documentos, fila de espera e privacidade |

O menu principal tem no máximo quatro atalhos. As áreas complementares ficam agrupadas por objetivo, com busca pelo nome ou descrição da tarefa. Cada perfil recebe somente seus atalhos; a autorização dos endpoints continua sendo conferida pelo servidor.

As áreas da equipe são **Rotina**, **Cadastros**, **Agenda e recursos**, **Gestão** e **Configuração**. As áreas complementares começam recolhidas. A busca desconsidera acentos e abre os grupos encontrados. Limpar a busca restaura o agrupamento. O caminho da página oferece retorno às áreas do sistema.

Na página inicial da equipe, os atalhos destacam recepção do dia, mensagens e pedidos de consulta. Cadastros e análises ficam nas áreas correspondentes. A recepção mostra as consultas do dia selecionado, com um filtro de data; paciente e profissional mantêm seus próprios acompanhamentos. No acompanhamento do atendimento, salas e indicadores são opções complementares recolhidas. A Central de Atendimento conserva suas três colunas, canais, ícones e operações. A assistente administrativa consulta as especialidades e os profissionais cadastrados, sem restringir os agendamentos à Psicologia e sem oferecer atendimento clínico pelo chat.

## Cadastro profissional

O domínio compartilhado agora é `ProfissionalSaude`. Os nomes de código antigos `Medico` e `Psicologo`, as rotas e a tabela física `Medicos` permanecem como compatibilidade para preservar dados e integrações.

O cadastro informa profissão, especialidade, conselho, número e região do registro, além da duração e agenda. A duração é configurável entre 5 e 240 minutos; novos cadastros começam com 30 minutos. As durações existentes são preservadas. Medicina usa CRM e UF; Psicologia usa CRP com regional/número; outras profissões de saúde informam o conselho e a UF correspondentes. O campo de especialidade continua dinâmico, compartilhado por toda a clínica. A categoria “Outra profissão de saúde” admite outras atuações sem impor CRP; não constitui certificação de suporte a todos os procedimentos clínicos dessas profissões.

A conferência documental permanece uma ação explícita do administrador, com fonte e responsável. Validação de formato não substitui consulta ao conselho. Alterações de registro exigem nova conferência. O sistema não consulta automaticamente todos os conselhos nem decide habilitação regulatória de uma modalidade de atendimento.

Não se converte CRM em CRP. Cadastros antigos com CRP continuam identificados como Psicologia e preservam sua conferência anterior. Cadastros legados com CRM sem profissão classificada conservam o número original; a equipe deve revisar profissão, conselho, número e UF antes da liberação clínica. A profissão de um cadastro com consultas não é reclassificada para outra atuação, preservando o sentido do histórico.

## O que é compartilhado e o que é específico

| Compartilhado entre profissões | Específico de Psicologia |
| --- | --- |
| Agenda, duração, profissionais, especialidades e salas | Prontuário psicológico e material técnico reservado |
| Confirmação, cancelamento, remarcação e encaixe | Retificações e transferência cifrada de guarda do acervo psicológico |
| Preparação com formulários cifrados | Conferência do CRP e designação de responsável técnico para esse acervo |
| Fluxo de chegada, início e conclusão | Consentimento de vídeo com texto específico de Psicologia |
| Consulta online com identidade, autorização e janela de acesso | Telas e APIs do acervo restritas às condições próprias do módulo |
| Canais, atendimento administrativo, privacidade e indicadores | |

Atendimentos de outras profissões usam identificação e consentimento de vídeo gerais. Não recebem acesso ao módulo de prontuário psicológico por terem um perfil de profissional. O histórico psicológico existente mantém seus envelopes, autores, destinatários e regras de acesso.

Esta versão não transforma o prontuário psicológico em prontuário universal. O adaptador administrativo para PEP agora inclui o identificador profissional correspondente quando cadastrado. Documentos e protocolos clínicos específicos de outras profissões exigem a implementação/homologação do módulo ou PEP correspondente. Os fluxos de atendimento de menores preservam a exigência operacional de conferência de responsável; a clínica deve avaliar a documentação e os requisitos da atuação.

## Aplicativo

O aplicativo do paciente **28.0.0** mantém as quatro áreas de início, consultas, chat e conta. A escolha do profissional passa a exibir o registro retornado pelo servidor (por exemplo, CRM e UF ou CRP), mantendo leitura dos contratos antigos. Preparação, consulta online, recuperação de chaves e a separação entre conversa atual e histórico continuam no fluxo existente.

## Atualização

`Database__ApplyV35Patch=true` é o padrão. A migração V35 é aditiva e reaplicável: acrescenta os campos de profissão/registro na tabela existente e o índice por conselho, UF e número. Não exclui consultas nem cadastros e não converte CRM em CRP. Se os patches automáticos estiverem desativados, aplique `docs/sql/V35-Multiprofissional.sql` depois dos patches anteriores, pelo procedimento de atualização do banco.

Não há credencial de provedor nova. O APK utiliza a mesma chave de assinatura. Instale o APK 28.0.0 por cima da versão anterior para atualizar, preservando os dados locais e as chaves; não é necessário desinstalar o aplicativo. A implantação no Render não foi executada por esta entrega. Os documentos das versões anteriores descrevem o histórico; este documento descreve a organização atual.

## Roteiro rápido para a equipe

1. **Marcar ou remarcar:** Agenda. **Resolver um pedido pendente:** Menu → Rotina → Pedidos de consulta.
2. **Paciente chegou:** Recepção → consulta → validar presença e conferir os dados necessários.
3. **Responder pelos canais:** Mensagens. A rotina da assistente e da revisão humana continua no atendimento.
4. **Cadastrar profissional:** Menu → Cadastros → Profissionais. Depois, o administrador confere o registro no conselho.
5. **Ajustar horários ou ausências:** Menu → Agenda e recursos. Salas presenciais também aparecem no acompanhamento da recepção.
6. **Analisar resultados:** Menu → Gestão. **Configurar provedores/perfis:** Menu → Configuração, no perfil administrador.

Continuam externas a coleta de resultados reais do piloto, homologação com PEP/provedores, avaliação clínica e de governança, testes físicos de chamadas/TalkBack e aceite da empresa/escola. Ampliar a gestão não certifica automaticamente a conformidade de todos os atos clínicos de cada profissão.
