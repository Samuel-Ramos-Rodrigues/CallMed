# Plano de correções por prioridade e dependência

1. **P1 — Gate de compilação**: aplicar `CMF-001`, executar `dotnet restore CallMed.sln`, `dotnet build -c Release`, `dotnet publish ...` e testes .NET disponíveis em homologação. Não publicar se falhar.
2. **P1 — CI real**: manter `dotnet build` obrigatório, além do teste estrutural `CMF-002`, e reexecutar PostgreSQL tests com DB isolado.
3. **P1 — Paridade Android**: comparar app Kotlin com APK, verificar 401/403 e aumentar `versionCode` antes de novo release assinado localmente.
4. **P1 — Fluxos SENAI**: testar concorrência de duas reservas, oferta expirada, duplicidade de webhook, recuperação da fila, confirmação multicanal, check-in e teleconsulta.
5. **P2 — Segurança**: testar autorização/IDOR/CSRF, sanitização de logs, Data Protection e consentimento clínico, com dados sintéticos.
6. **P2 — UX**: revisar telas reais nos quatro perfis em zoom 100/200%, mobile e desktop; corrigir CSS apenas a partir de evidências observadas.
7. **P3 — Linguagem**: mensagens de profissional devem ser multiprofissionais; ajuste incluído neste pacote.

Nenhuma migração ou modificação de banco introduzida neste checkpoint.
