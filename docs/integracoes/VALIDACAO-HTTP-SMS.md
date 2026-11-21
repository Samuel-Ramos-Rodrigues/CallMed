# Validação da integração httpSMS — 03/11/2026

- 298 verificações automatizadas aprovadas no projeto `tests/CallMed.Checks`.
- `dotnet publish src/CallMed.Web/CallMed.Web.csproj --configuration Release` concluído com sucesso em .NET 8.
- `node --check` aprovado em `atendimento.js` e `service-worker.js`.
- Casos cobertos: API Key/From, payload de envio, autenticação JWT HS256, rejeição de tokens inválidos, deduplicação, persistência antes da IA, retomada após recriação do contexto, atendimento humano, solicitações de agendamento e eventos de status.
- API aceitar uma mensagem mantém o estado na fila; entrega exige confirmação do evento correspondente.
- A publicação ainda exibe dois avisos já presentes: nulabilidade em ForgotPassword.cshtml.cs e função local não utilizada em Views/Atendimento/Index.cshtml.
- Testes com HTTP simulado e banco em memória: nenhum SMS real foi enviado, nenhuma credencial da conta foi utilizada e não foi feito deploy no Render.
- O código dos provedores WhatsApp/Evolution e e-mail/Gmail/SMTP, o validador genérico de webhook e as migrações permanecem iguais aos da versão-base.

Configuração operacional: [HTTP-SMS.md](HTTP-SMS.md).
