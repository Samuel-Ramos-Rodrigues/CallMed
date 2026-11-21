# CallMed V27 — medição dos benefícios da demanda

Entrega de 09/11/2026. Versão do site/backend V27; Android permanece **26.1.0**, com o mesmo APK, assinatura e hash da V26.1. A implantação no Render não foi realizada nesta entrega.

## O que foi acrescentado

| Requisito da demanda | Como medir agora | Fonte |
| --- | --- | --- |
| Redução de faltas | Comparação antes/depois com diferença em pontos percentuais | Contagens separadas de realizadas, ausentes, canceladas e sem desfecho |
| Ocupação da agenda | Minutos agendados não cancelados / capacidade profissional disponível | Agenda no piloto; escala efetiva conferida pela equipe |
| Menor esforço administrativo | Minutos de recepção por agendamento | Tempo medido pela equipe para as consultas da data selecionada |
| Custo administrativo | Custo proporcional ao tempo, total e por agendamento | Minutos medidos e custo real da hora, informado pela clínica |
| Evidência dos resultados | JSON com método, cobertura, valores, fontes, motivos e impressão da agenda | Revisão vigente de cada dia; mesma leitura usada no cálculo e na exportação |

A ocupação é utilização da **agenda**, incluindo faltas e consultas sem desfecho. Não é taxa de presença nem tempo de atendimento efetivo. Taxa de aceite de ofertas continua com denominador próprio no painel de indicadores.

## Como usar

1. Entre como **Admin ou Funcionário**. Em **Fluxo de atendimento**, acesse **Registrar medição conferida** ou **Comparar antes e depois**. O painel de resultados também contém esses links.
2. Selecione um dia encerrado e a etapa **Antes** ou **Piloto**. O escopo é toda a clínica, com o mesmo método nos dois períodos.
3. Na linha de base, declare separadamente os quatro desfechos. Não transforme “consultas previstas menos faltas” em consultas realizadas. Os minutos agendados são opcionais se não houver uma fonte confiável.
4. No piloto, confira a prévia extraída da agenda. O servidor recalcula os totais ao salvar e exige nova conferência se houver mudança desde a prévia. A duração vem da jornada quando existente e, caso contrário, da consulta. Consultas canceladas não somam minutos.
5. Informe a capacidade efetiva dos profissionais, o tempo administrativo e o custo/hora apenas quando medidos. A capacidade é a soma de minutos disponíveis para consultas na escala do dia, descontando pausas/bloqueios; não some duas vezes um horário de um mesmo profissional. O tempo administrativo deve se referir às consultas dessa data, incluindo tarefas feitas antes (por exemplo, confirmação) ou pelos canais. Para equipes com custos distintos, use custo/hora ponderado pelo tempo registrado e descreva o método na fonte.
6. Identifique a fonte, o motivo e confirme a conferência. Não inclua nomes de pacientes, dados pessoais ou conteúdo clínico nesses campos. Zero significa ausência de atividade **verificada**; campo opcional vazio significa dado desconhecido.
7. Escolha períodos comparáveis de até 366 dias encerrados, com a linha de base terminando antes do piloto. O painel mostra dias medidos, dias sem dados e as amostras específicas de cada indicador. Registre dias fechados com zeros conferidos, quando fizerem parte do intervalo estudado.

## Fórmulas e limites

- Faltas: `ausentes / (realizadas + ausentes) × 100`. Cancelamentos e consultas sem desfecho são apresentados separadamente.
- Ocupação: `soma dos minutos agendados / soma dos minutos disponíveis × 100`, somente nos dias com os dois valores conhecidos. Não é uma média simples das taxas diárias. Acima de 100% indica excesso de agendamento e pede conferência.
- Recepção por agendamento: soma dos minutos administrativos dividida pelo número de consultas dos **mesmos dias** com tempo medido. Inclui canceladas, que também podem gerar trabalho administrativo.
- Custo: `minutos administrativos × custo/hora / 60`. A média por agendamento usa só os dias com tempo e custo/hora conhecidos. Valores monetários têm duas casas decimais; a entrada aceita ponto ou vírgula decimal sem separador de milhar.
- Diferença: **Piloto menos Antes**. Taxas em pontos percentuais; tempo e custo nas respectivas unidades por agendamento. Sem denominador, o indicador é “Sem amostra”. Dias sem registro não viram zeros.

Diferenças de composição dos pacientes, duração das sessões, profissionais, dias da semana e completude dos desfechos influenciam os resultados. A comparação é descritiva, não demonstra causalidade nem certifica redução de custos. Os dados de teste são fictícios e não representam resultados reais da CallMed ou da empresa.

## Correções, histórico e acesso

Cada dia/etapa tem revisões numeradas. Corrija recarregando o dia, informando o motivo e registrando nova revisão. O banco e o contexto EF impedem editar ou apagar revisões publicadas. A comparação usa apenas a última revisão; nunca soma duas revisões do mesmo dia. Formulário antigo ou disputa simultânea gera conflito, sem sobrescrever o trabalho de outra pessoa.

Se o dia ou etapa foi registrado por engano, marque **Desconsiderar este dia na comparação**, informe o motivo e confirme. Isso cria uma revisão de retirada, mantendo a evidência e excluindo o dia da amostra. Para restaurar, carregue o dia, preencha/confira os valores e registre nova revisão com a opção desmarcada. A retirada não ressuscita os valores de revisões antigas nem cria um dia de atividade zero.

Medições antigas de “consultas previstas” são preservadas no painel original como histórico. Não são convertidas para os novos denominadores. Os registros novos guardam usuário e horário UTC da conferência; a exportação omite identificadores de usuários e não exporta identificadores de pacientes ou conteúdo clínico. A fonte e o motivo são texto declarado pela equipe: mantenha-os sem dados pessoais.

As telas, registros e exportações são restritos a Admin/Funcionário, exigem autenticação e têm cache privado desabilitado. Os formulários de registro usam proteção antifalsificação. O conteúdo das sessões e o fluxo criptográfico clínico permanecem fora dessa medição.

## Atualizar no Render

O projeto mantém as configurações anteriores. A única opção nova é `Database__ApplyV34Patch`, **true por padrão**; nenhuma credencial nova é necessária. A inicialização aplica a V34 depois das anteriores. A V34 cria `MedicoesPiloto`, seu índice de revisões e a proteção de imutabilidade, sem alterar consultas ou medições legadas. O SQL reaplicável está em `docs/sql/V34-Piloto.sql` e corresponde ao inicializador.

Se a sua implantação desabilita patches automáticos, aplique esse SQL pelo procedimento de atualização do banco antes de usar o painel. O script considera o esquema das versões anteriores já instalado. Faça a atualização a partir do projeto completo e mantenha sua conexão e segredos atuais. A distribuição no site continua servindo o APK 26.1.0; esta revisão não exige reinstalação do aplicativo.

## O que depende de validação externa

O recurso de medição está implementado, mas a linha de base, os tempos, as capacidades, os custos e os resultados do piloto precisam ser coletados pela clínica. Também continuam externos o aceite da empresa/escola da adaptação para Psicologia, revisão clínica e de governança, homologação do PEP escolhido e os ensaios físicos de chamadas/TURN/TalkBack descritos em `ACEITACAO.md`. Esta versão não marca esses itens como concluídos nem altera sua aprovação.
