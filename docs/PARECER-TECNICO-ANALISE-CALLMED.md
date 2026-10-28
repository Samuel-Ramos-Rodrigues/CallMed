# Parecer técnico — CallMed (Acessos Médico/Atendente)

**Objetivo:** registrar o resultado da análise estática do projeto e organizar as ações recomendadas para homologação do fluxo de agendamento.

## Resumo executivo

O projeto apresenta uma base técnica promissora para centralizar solicitações e agendamentos, com ASP.NET Core MVC/.NET 8, Razor/JavaScript, Entity Framework Core, PostgreSQL, autenticação por perfis, PWA e recursos de contingência. A estrutura contém módulos de agenda, solicitações de atendimento, histórico, auditoria e integrações de comunicação.

Este parecer não representa certificação de segurança ou comprovação de funcionamento em produção. A análise foi predominantemente estática e não foi possível compilar o backend neste ambiente porque o SDK do .NET não estava instalado.

## Pontos positivos observados

- Separação entre controladores, serviços, modelos e configurações de persistência.
- Uso de autenticação e perfis de acesso.
- Há mecanismo de bloqueio transacional PostgreSQL (`pg_advisory_xact_lock`) no serviço de agendamento, além de índices únicos configurados para impedir conflitos em registros relevantes.
- Há tratamento de chave de contingência para solicitações e documentação de homologação.
- O service worker evita tratar páginas clínicas dinâmicas como conteúdo estático cacheável.
- Há opções de acessibilidade na interface e fluxo para contingência offline.
- Existe roteiro de homologação em `docs/ROTEIRO-HOMOLOGACAO-V22.md`.

## Achados e recomendações priorizadas

### P0 — Segurança e privacidade

1. **Autenticação de webhooks — corrigido no código:** os endpoints aceitam o segredo apenas nos cabeçalhos `X-CallMed-Webhook-Secret` ou `X-MKSAN-Webhook-Secret` (compatibilidade legada). Segredos em query string são rejeitados. Atualize qualquer integração antiga que ainda envie `?secret=...`.
2. **Autorização por paciente e profissional:** executar testes negativos nas páginas e APIs, incluindo acesso direto a identificadores de outros pacientes, consultas sem vínculo autorizado e sessão expirada.
3. **LGPD e operação:** revisar minimização de dados, base legal e finalidade, retenção, acesso aos registros, trilha de auditoria, backups, resposta a incidentes e compartilhamento com provedores externos. A avaliação deve envolver os responsáveis pela operação e proteção de dados.
4. **Proxies e cabeçalhos encaminhados — corrigido no código:** o sistema não limpa mais a lista de proxies/redes confiáveis padrão e permite adicionar IPs de proxy em `ReverseProxy:KnownProxies`. Em produção, preencha essa lista com os IPs exatos dos proxies reversos confiáveis. A lista vazia é deliberada e não deve ser preenchida com IPs de clientes.

### P0 — Integridade dos agendamentos

5. **Concorrência real no PostgreSQL:** executar duas reservas simultâneas para o mesmo médico/data/horário; somente uma pode ser confirmada. Conferir transação, índices únicos, resposta amigável e ausência de registros parciais.
6. **Idempotência:** repetir uma solicitação após timeout/resposta perdida e reenviar eventos de webhook. O sistema deve responder de forma consistente sem criar duplicatas.
7. **Contingência offline:** desconectar a rede, registrar solicitação, fechar e reabrir a aplicação e reconectar. Confirmar persistência, proteção da fila, sincronização e tratamento de conflito. Não indicar consulta confirmada antes da confirmação do servidor.

### P1 — Integrações e notificações

8. Validar Evolution API/webhooks, rejeição de segredo inválido, eventos duplicados e eventos de grupo não aplicáveis.
9. Testar as janelas e o agendamento dos lembretes documentados, incluindo falhas no SMTP/SMS, retries, registro de entrega e ausência de duplicação.
10. Confirmar que as integrações com o HIS existente estão especificadas para o sistema real. A existência de interfaces genéricas não comprova uma integração funcional com qualquer HIS.

### P1 — Usabilidade e inclusão

11. Realizar testes acompanhados com idosos, pessoas com baixa alfabetização digital e atendentes. Medir sucesso sem ajuda, abandono, tempo de conclusão, compreensão da confirmação e facilidade para corrigir dados.
12. Preservar telefone e atendimento presencial como canais assistidos, garantindo que a equipe registre as solicitações no fluxo centralizado.
13. Verificar contraste, ampliação de texto, navegação por teclado/leitor de tela, mensagens de erro, foco e leitura por voz em dispositivos reais.

### P2 — Métricas e sustentabilidade

14. Medir situação inicial e pós-implantação para tempo de agendamento/confirmacão, taxa de faltas, retrabalho, tempo de atendimento da recepção, conflitos/duplicatas e uso de papel.
15. Acompanhar custos de hospedagem, banco de dados, mensageria, e-mail, SMS, serviços de IA, domínio, backups e suporte, conforme a documentação existente.

## Alterações aplicadas ao código nesta revisão

- A validação do segredo dos webhooks foi extraída para `Services/Atendimento/AtendimentoWebhookSecretValidator.cs` para permitir testes diretos e reutilização.
- Os três endpoints de webhook passaram a aceitar segredo somente em cabeçalho e receberam limite de requisição de 1 MiB.
- A configuração de `ForwardedHeadersOptions` deixou de confiar em qualquer origem. IPs adicionais de proxy devem ser informados em `ReverseProxy:KnownProxies`.
- Foram adicionados testes ao projeto `tests/CallMed.Checks` para segredo correto, segredo incorreto, segredo ausente, cabeçalho legado e rejeição de segredo na query string.
- As instruções de integração Evolution foram atualizadas para documentar o cabeçalho obrigatório e a configuração do proxy confiável.

## Verificações executadas neste ambiente

- A análise sintática (`node --check`) foi concluída com sucesso para 14 arquivos JavaScript em `CallMedCrud/wwwroot`.
- Foram conferidos estaticamente os caminhos listados pelo service worker.
- Não foi executada compilação .NET: o comando `dotnet` não estava disponível.
- Não foi executado o teste DOM: a dependência `happy-dom` não estava instalada no ambiente; `npm ci` também não é aplicável sem `package-lock.json`, e a tentativa de gerar o lockfile não concluiu por indisponibilidade/timeout do registro de pacotes.
- Não foram executados os testes de navegador Playwright/Chromium nem o novo conjunto de testes C# de webhooks; as dependências/SDK necessários não estavam disponíveis neste ambiente.
- Não foram testados banco PostgreSQL real, credenciais de serviços externos, mensagens reais ou agendamentos em produção.

## Comandos sugeridos para validação local

Na pasta da solução:

```bash
dotnet --info
dotnet restore CallMed.sln
dotnet build CallMed.sln --configuration Release
dotnet run --project tests/CallMed.Checks/CallMed.Checks.csproj
```

Para os testes JavaScript:

```bash
cd tests
npm install
npm run test:dom
npm run test:browser
```

O projeto `tests/CallMed.Checks/CallMed.Checks.csproj` está configurado como executável de verificações (`OutputType` Exe), portanto o comando sugerido para executá-lo é `dotnet run`.

Após a execução, seguir os cenários de `docs/ROTEIRO-HOMOLOGACAO-V22.md`. Utilizar somente dados fictícios em ambiente de homologação; testar integrações reais em ambiente controlado.

## Conclusão

Recomenda-se manter a arquitetura existente e priorizar a validação de segurança, autorização, concorrência, idempotência e contingência antes de uma implantação real. Em seguida, homologar integrações e medir a experiência de usuários. Os ganhos de eficiência devem ser apresentados como resultados medidos, não como percentuais presumidos.

**Nota sobre validação:** algumas recomendações dependem de infraestrutura e operação reais e permanecem pendentes: compilação/testes .NET, concorrência no PostgreSQL, teste completo da fila offline, validação de permissões em ambiente integrado, testes de Evolution/SMTP/SMS/HIS, revisão operacional de LGPD e testes de usabilidade com pacientes e atendentes. Não declare o sistema pronto para produção até concluir a homologação.
