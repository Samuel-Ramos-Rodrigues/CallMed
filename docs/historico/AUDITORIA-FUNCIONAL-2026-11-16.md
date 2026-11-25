# Auditoria funcional e correções — 16/11/2026

Base: CallMed-NEXT-V54.1-VISUAL-VIDEO.zip. Esta rodada parte das duas capturas do agendamento enviadas pelo usuário e amplia a validação para os módulos existentes.

## Falhas encontradas e corrigidas

1. **Calendário estreito e números quebrados.** O contêiner de datas ainda usava três colunas para o antigo seletor de dias. O calendário ocupava uma única coluna e seus botões herdavam o espaçamento das opções grandes. Agora utiliza toda a largura, com sete colunas alinhadas para dias e cabeçalho, números sem quebra, navegação de mês organizada e botões delimitados. As cinco etapas têm composição própria e os cartões de profissionais organizam avatar, nome, especialidade e seta.
2. **Respostas fora de ordem no agendamento.** Trocar profissional/data enquanto a consulta anterior ainda carregava podia substituir as novas opções por dados antigos. Adicionados identificadores de requisição e validações da seleção atual para profissionais, datas e horários; a seleção anterior é invalidada imediatamente. A busca de primeira vaga não continua aplicando resultados após mudança manual. A consulta de convênio também descarta respostas de pacientes anteriores.
3. **Confirmação da equipe sem paciente escolhido.** O botão só considerava especialidade/profissional/data/horário. Agora também exige paciente no formulário da equipe; a validação de completude serve ao formulário de edição compartilhado.
4. **Remarcação com horários de outra seleção.** A resposta de horários agora é conferida com a data/requisição atual antes de atualizar o seletor. Os rótulos foram associados aos campos.
5. **Jornada mostrando horário antigo.** A chave de atualização considerava apenas ID/modalidade/encerramento. Agora inclui data, horário e identificação dos participantes, reconstruindo o cartão quando esses dados mudam. Acrescentados os estilos das barras, seções, estados vazios, indicadores e links de gestão que existiam nas views, mas não tinham cobertura de CSS adequada.
6. **Elementos novos sem estilo correspondente.** Restaurada a organização visual do quadro e dos cartões de solicitações; corrigidos os indicadores da agenda, as nove colunas da disponibilidade e os cartões de próximos pacientes do profissional. As tabelas preservam largura legível dentro da rolagem local.
7. **Controles duplicados ou desproporcionais.** O upload customizado exibia também o controle nativo. O campo nativo agora ocupa a área de interação sem duplicar o visual, mantendo acesso por teclado. Corrigida a altura do botão de cadastro de salas, os indicadores de estado dos canais, a apresentação das prioridades da espera e os marcadores expansíveis do menu de funções.
8. **Navegação e sobreposição.** Os nomes do menu móvel não quebram letra por letra com fonte ampliada; quando necessário a barra permite rolagem horizontal local. Reservado espaço para o assistente não cobrir os links do cartão de dados do paciente.
9. **Mensagens e nomes inconsistentes.** Mensagens de agendamento passaram de “psicólogo” para “profissional”. A listagem utiliza a especialidade do cadastro vinculado quando disponível, em vez de produzir nomes terminados em um travessão vazio.

O cache da PWA foi atualizado para `v107-auditoria-fluxos`.

## O que foi exercitado

| Área | Cobertura nesta rodada |
|---|---|
| Agendamento | Escolhas reais de área/profissional/data/horário; calendário renderizado em 320, 390, 768, 1280 e 1713 px; fonte 100%/200%; primeira vaga; criação, remarcação e cancelamento pelos formulários reais; resposta atrasada controlada; exigência de paciente na recepção |
| Consultas e jornada | Filtros/listagens, permissões e estados; atualização de horário sem recarregar a página; confirmação, preparação e etapas do atendimento |
| Solicitações e fila de espera | Organização por etapa, filtros, convênios, vínculos e ofertas; aceite, expiração, cancelamento e concorrência no PostgreSQL isolado |
| Atendimento multicanal/IA | Assumir/devolver, rascunhos, aprovação/descarte, progresso, falha de envio, polling durante confirmação e prevenção de envio duplicado; adaptadores de e-mail, Evolution e httpSMS com transportes simulados |
| Minha conta e acesso | Foto por formulário nativo, preferências, abas e teclado; validações de conta, recuperação de senha, sessões, vínculos e isolamento por perfil nos testes de serviço/API |
| Profissionais, pacientes e gestão | Cadastro de profissional pela interface, campos de registro, navegação por perfil, listas, horários, administração de salas e filtros |
| Teleconsulta | Teste e liberação dos dispositivos, entrada/saída, estados, início/conclusão, presença, autorização, janela de acesso e permissões; câmera e usuários sintéticos |
| Prontuário/psicologia | Chaves e cópias protegidas, envio/leitura de respostas cifradas, registros, retificação, guarda, exportação/importação FHIR e restrição do material reservado |
| Indicadores/piloto | Medições, revisão concorrente, acesso por perfil, comparação e dados operacionais |
| Offline/PWA | Cifragem local, senha incorreta, recarga, falha de armazenamento, resposta perdida, reenvio idempotente, acesso negado e não armazenamento de páginas clínicas no cache |
| Aplicativo/API móvel | Suítes de contratos e permissões da API, integridade do APK já incluído ; sem instalação em aparelho real |

## Resultado final

**1.777 verificações aprovadas:** 1.112 em serviços/PostgreSQL, 600 no navegador e 65 em testes adicionais. A revisão de 48 telas e a matriz de 168 composições terminaram sem falhas de carregamento, violações verificadas de acessibilidade ou transbordamento horizontal. As contagens são de verificações executadas, incluindo cenários de perfil, estado, resolução e tema, e não de funcionalidades distintas.

## Evidências e execução

Os resultados finais, logs de execução aprovados e contagens por suíte estão em `validation/funcional/`. O resumo legível por máquina é `validation/funcional/resumo.json`.

- Compilação Release e publicação local de Web/Worker.
- 15 suítes .NET, com 1.047 verificações; sete suítes PostgreSQL, com 65 verificações.
- Bateria de navegador com a aplicação Razor real e dados sintéticos, incluindo revisão das 48 telas, testes dedicados de fluxos, geometria e acessibilidade.
- Testes adicionais de polling/aprovação, avaliação do atendimento, contingência, FHIR e cache.
- Checagem de referências das 109 páginas Razor, sintaxe dos JavaScripts e das cinco folhas CSS.

Os testes que ainda buscavam estruturas antigas de painel, nomes de botões ou a folha removida `callmed.css` foram adaptados à interface atual. Os testes de geometria e comportamento continuam verificando os controles existentes. O novo `tests/browser/check-booking-flow.cjs` testa escolhas e envio reais, em vez de depender apenas da abertura da página. A matriz de CI inclui esse fluxo.

## Limites

A cobertura percorre os módulos e fluxos acima; não é uma prova de ausência de qualquer erro em toda combinação possível de dados. Os testes foram locais, com dados sintéticos. Não foram usados banco ou credenciais de produção, nem realizados envios reais de e-mail/WhatsApp/SMS. Conectividade TURN entre redes externas, configuração Render, instalação/assinatura Android e testes de certificado no Windows exigem seus ambientes correspondentes e não estão sendo declarados homologados nesta rodada. O APK original foi preservado.

Este documento atualiza a situação dos relatórios históricos incluídos no projeto: o verificador `check-design.cjs`, antes incompatível com os painéis atuais, foi atualizado e executado nesta revisão.
