# CallMed R9.3.13

## Mudanças para pacientes e equipe

| Fluxo | Comportamento verificado |
|---|---|
| Agendamento | O mesmo paciente pode marcar consultas consecutivas. Intervalos sobrepostos são recusados, mesmo com profissionais diferentes. |
| Remarcação | O sistema verifica a duração efetiva do atendimento. Se faltar tempo antes da consulta seguinte, o horário anterior é preservado. |
| Próxima consulta | O início do paciente mostra o próximo atendimento ativo pelo dia e horário da clínica. |
| Painel do profissional | A lista de vagas e o agendamento usam o mesmo cálculo para consultas, reservas temporárias e horários passados. |
| Encaixe | O paciente escolhe presencial ou por vídeo na lista de espera, nas confirmações e na jornada. A API pede uma escolha explícita. Um app antigo que envie o aceite sem modalidade recebe orientação para concluir pelo site. |
| Leitura | A Central, os formulários, os controles da agenda e a navegação móvel receberam correções de contraste. As páginas offline usam o mesmo CSS do site. |

## Validação

A compilação Release da solução .NET 10 e a publicação local do site passaram. Foram registradas 1.144 verificações aprovadas nas suítes .NET, incluindo sete suítes com PostgreSQL local. O teste concorrente usou duas conexões independentes para reservar o mesmo paciente em dois profissionais. Apenas uma consulta foi gravada.

A revisão no navegador percorreu 47 rotas, com dados fictícios e perfis diferentes. Cada rota foi verificada em quatro combinações de tema, largura e tamanho de texto, chegando a 188 resultados. O axe-core não apontou violações nos critérios automáticos selecionados e nenhuma combinação apresentou rolagem horizontal na página. Testes em dispositivos físicos e com tecnologias assistivas complementam essa cobertura.

Para repetir as regressões específicas de agenda a partir da raiz:

```sh
dotnet run --project tests/CallMed.Checks -c Release -- --agenda-fluxos
```

A suíte `--encaixe-pg` exige um PostgreSQL local dedicado com banco `callmed_test_encaixe`, além de `CALLMED_TEST_DB` e `CALLMED_TEST_RESET=SIM` no ambiente de teste. Ela recria esse banco. O ambiente do Render continua usando as configurações já existentes.

## Distribuição

O ZIP contém uma única árvore de código. Foram retirados scripts antigos de reaplicação de patches, instalador de teste duplicado, relatórios gerados, mapas de ofuscação históricos e folhas de estilo substituídas. Os mapas das versões anteriores continuam recuperáveis no ZIP de origem.

O site inclui o APK assinado 50.0.0. O código Android 54.1.3 contém a escolha de modalidade. A compilação da nova versão Android não foi concluída neste ambiente, onde o Gradle Wrapper não conseguiu baixar a distribuição. A etapa de release requer a chave original do aplicativo. O script de preparação e a publicação do APK estão descritos no [guia Android](../../../mobile/android/README-ANDROID.md).
