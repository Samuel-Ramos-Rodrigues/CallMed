# CallMed V35 — adequação à demanda SENAI Joinville Sul

## Objetivo
Apoiar a demanda "Plataforma Inteligente de Gestão e Otimização de Consultas Médicas" usando os fluxos reais do CallMed: agendamento multicanal, confirmação, remarcação, lista de espera, pré-cadastro/check-in, profissionais/salas e teleconsulta.

## Alterações realizadas neste pacote

1. **Navegação por perfil reduzida**: quatro atalhos por papel (três para profissional) no topo/celular. Todas as demais funções continuam no buscador de áreas (Ctrl+K), sem remoção de controllers ou serviços.
2. **Painel da recepção reconstruído**: ações para agendar, check-in, mensagens e encaixe, indicadores calculados do banco, lista de próximas consultas e prioridades operacionais. Inclui o fluxo agendamento → confirmação → fila → pré-atendimento → presencial/teleconsulta, com links reais.
3. **Paciente**: pré-atendimento/check-in acessível diretamente da tela inicial e do modo simplificado. Preservadas preferências de acessibilidade, cadastro e jornada existente.
4. **Fila de espera — correção de estado**: exibe uma "vaga encontrada" somente se houver oferta aberta e ainda válida, com data, horário e hora-limite; cancelamento do pedido revoga suas ofertas abertas. Não é permitido aceitar oferta vencida por este fluxo.
5. **Integração de e-mail**: texto da tela passou a refletir a configuração real de envio administrativo automático, distinguindo casos sensíveis e rascunhos sujeitos à revisão. Gmail API e Brevo SMTP não foram substituídos.
6. **Terminologia multiprofissional**: mensagens gerais da lista de espera deixam de exigir exclusivamente psicólogo.
7. **Segurança de distribuição**: removidos do ZIP `mobile/android/callmed-upload.jks` e `mobile/android/keystore.properties`, que continham material de assinatura. Incluído `keystore.properties.example` sem segredos. A assinatura de novos APKs RELEASE exige restaurar a chave **privadamente**; não gerar outra para atualizações sem verificar a política da loja.
8. **Estilo único**: complementos responsivos e de acessibilidade em `src/CallMed.Web/wwwroot/css/callmed.css`, sem adicionar segunda folha CSS. Central de Atendimento continua com as três colunas e mesma lógica.

## O que já estava implementado e foi preservado

- Gestão de agenda, profissionais, horários, salas, bloqueios e exceções.
- Worker de lembretes de consulta (aproximadamente 24h e 2h), com links protegidos de confirmar/cancelar/remarcar e fallback para equipe por telefone.
- Cancelamento libera vagas e chama mecanismo de reserva de encaixe; worker de espera reprocessa ofertas/expirações a cada 30 segundos.
- Formulário clínico protegido, conferência de dados, convênio e check-in na Jornada.
- Teleconsulta WebRTC, gestão de consentimento e acesso clínico por papel.
- WhatsApp Evolution, SMS httpSMS, entrada Gmail API, saída Brevo SMTP e IA.
- PEP parcial / FHIR, relatórios, métricas piloto e auditoria.

## Testes obrigatórios ANTES da apresentação/homologação

1. Banco PostgreSQL com dados de teste e `dotnet build CallMed.sln -c Release`.
2. Autenticação de paciente, médico/psicólogo, atendente e admin; acesso por URL direta de cada perfil.
3. Mensagens reais dos canais configurados: lembrete, confirmação pelo link seguro, remarcação, cancelamento e reentrada na agenda.
4. Concorrência: dois pacientes tentando a mesma vaga; expiração e aceite da oferta de espera, prioridade de candidatos e recusa.
5. Pré-cadastro e check-in em celular (incluindo fonte ampliada, teclado e leitor de tela), conferência na recepção e consulta médica.
6. Teleconsulta com dois dispositivos/redes e TURN; integração PEP testada com fornecedor real.
7. Privacidade e conformidade com LGPD e normas profissionais (inclusive Psicologia): revisar políticas, criptografia de dados em trânsito/em repouso, gestão de consentimento, auditoria, retenção, fornecedores e acessos.
8. **Criptografia ponta a ponta:** não declarar implementada para todos os canais de terceiros sem avaliação/prova técnica do transporte e dos pontos de descriptografia.
9. Piloto: medir faltas/no-show, ocupação, tempo de recepção, vagas recuperadas e satisfação em dois períodos. Evitar dados fictícios.

## Pendências de infraestrutura

- O deploy precisa manter os jobs em execução (embutidos no site ou `CallMed.Worker` em modo adequado) e os canais integrados/configurados.
- A URL HTTPS pública deve estar correta em `RENDER_EXTERNAL_URL` ou `Twa__Origin` para gerar links confiáveis.
- A chave original de upload do Android, se necessária para releases existentes, deve ser recuperada pelo responsável de maneira privada. O arquivo não acompanha mais o projeto.

## Validação local desta entrega

- `python scripts/check-css.py` — CSS único e referências.
- `python scripts/auditar-views-callmed.py` — referências MVC/static.
- `node --check` nos scripts web.
- `python scripts/validar-demanda-senai.py` — invariantes da personalização e ausência de chaves privadas na distribuição.
- `unzip -tq` no pacote final.

**Limites:** neste ambiente não há SDK .NET nem serviços de banco/canais para executar testes integrados; portanto não se declara a solução integralmente homologada ou certificada para LGPD/CFM/ANS/CFP. O código anterior já continha os fluxos principais, mas a aceitação formal depende dos testes descritos.
