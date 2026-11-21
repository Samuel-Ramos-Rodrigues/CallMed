# Riscos residuais e bloqueios

- **Crítico para o deploy:** o erro CS0103 foi corrigido no fonte, mas `dotnet publish` ainda não pôde ser executado nesta sessão. Não promover código sem build real.
- **Consulta e encaixe:** testes concorrentes de banco sob duas transações, ofertas vencidas e webhooks duplicados não executados; dados sintéticos e PostgreSQL isolado necessários.
- **Privacidade clínica/psicológica:** autorizações, escopo de prontuário, retenção, consentimento e trilha de auditoria exigem revisão contextual completa e validação especializada.
- **Teleconsulta:** testes de mídia, ICE/TURN, autorização e reconexão com dois aparelhos e redes não executados.
- **Android:** ausência de prova que APK V50 contenha todo código-fonte Kotlin atual; release novo depende de assinatura local.
- **UX:** não foi iniciado Web com contas sintéticas; sem conclusão sobre WCAG 2.2 AA ou funcionamento responsivo real de 109 telas.
- **Provedores:** sem credenciais para simular ciclo completo Gmail/Brevo/httpSMS/Evolution/Gemini.
- **Cobertura:** revisão semântica integral exigida pelo prompt ainda não atingiu 100%; consultar `progresso-auditoria.md`.
