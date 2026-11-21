# CallMed NEXT V54.1.4 — Correção de fluxos por perfil

Base: **CallMed NEXT V54.1.3**, incluindo os ajustes anteriores de agenda, alertas, notificações e botões-link. Não há novas dependências, migrações de banco nem alterações em integrações.

## Jornadas ajustadas

- **Paciente:** principal destino de “Consultas” passa a ser a Jornada, com próximos passos por consulta. Histórico e remarcações continuam em `Consulta/Index`. O atalho “Teleconsulta” filtra apenas as consultas online (`Jornada?modo=online`) e oferece retorno à lista completa. A consulta futura da home abre a Jornada daquela consulta. O agendamento deixa explícito que marcar a consulta e confirmar presença são etapas distintas.
- **Médico:** o próximo atendimento, as consultas do dia e os próximos pacientes abrem `/Jornada/Consulta/{id}` em vez de redirecionar para o histórico de exames. O histórico segue como link secundário. Botões de concluir diretamente do dashboard foram removidos para conduzir à tela onde o início/conclusão seguem as validações da Jornada.
- **Recepção:** painel diário organizado em “Exceções”, “Confirmações”, “Solicitações” e “Encaixes”. Validação manual de chegada apresentada como exceção; chegada registrada e pagamento conferido não apresentam ações redundantes.
- **Solicitações:** contadores são atalhos reais para as três etapas. Links de confirmação apontam para a central apropriada.
- **Lista de espera:** texto esclarece que entrar na fila não agenda automaticamente; aceite de oferta na própria lista continua acessível.
- **Central de Atendimento:** explicação recolhida dos estados da IA, sem modificação das regras de envio/rascunho.
- **Jornada individual:** altera modalidade dentro de seção opcional; check-in é disponibilizado visualmente **somente** dentro da janela de 24h anteriores até 4h posteriores e após conferir o pré-cadastro, acompanhando as regras existentes de `JornadaService.MoverAsync`. Evita um botão que inevitavelmente retornaria erro fora da janela.

## Segurança funcional

- Os endpoints e as regras de permissão permanecem inalterados.
- Nenhuma ação POST passou a ser executada automaticamente nem por link GET.
- Ações destrutivas e envio de comunicações não foram automatizados.
- O filtro de teleconsulta é apenas visual, aplicado sobre as consultas já autorizadas pelo servidor.

## Validação sugerida antes do deploy

1. Executar `dotnet restore CallMed.sln` e `dotnet build CallMed.sln -c Release` (requer .NET SDK local).
2. Paciente: iniciar → próxima consulta → confirmar presença → pré-cadastro → check-in; testar antes e dentro da janela, presencial e online; voltar à lista e abrir teleconsultas.
3. Médico: meu dia → abrir próximo atendimento → acessar vídeo (se online) → iniciar → concluir; verificar que o histórico de exames só abre pelo link secundário.
4. Atendente: painel → exceções → consulta → confirmar pendência → solicitar horário → confirmar → encaixe.
5. Admin: conferir indicadores e links para operação, gestão, consultas e agenda; checar permissões.
6. Testar responsividade em 390px, 768px e desktop, temas claro/escuro, teclado e leitores de tela.
7. Repetir testes automatizados de backend, navegador e PostgreSQL da instalação local. Testar integrações Evolution/Brevo/httpSMS somente em ambiente de testes, pois dependem de provedores externos.

> Os testes realizados neste ambiente são estáticos; não substituem build, testes automatizados e inspeção autenticada após deploy.
