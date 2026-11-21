# Checkpoint da auditoria — 16/11/2026 (referência do log do deploy)

- Base: 724 arquivos.
- Inventariados: **724/724 (100%)**.
- Textuais classificados e escaneados linha a linha automaticamente: **684/684**, **63670 linhas**.
- Revisão contextual completa: **16/724 (2.21%)**.
- Revisão contextual parcial: **6/724**.
- Restantes não revisados semanticamente: **702**.
- Binários: 40; apenas metadados verificados, sem decompilação.
- Achados classificados: **7** (3 corrigidos no fonte/teste; 4 riscos ou bloqueios ainda abertos).
- Testes: ver `relatorio-testes.md`.
- Bloqueios: sem SDK .NET 10 e PostgreSQL de homologação; sem credenciais dos provedores; sem instalações físicas; sem instância autenticada da Web.
- Próxima trilha: auditoria semântica integral dos serviços de atendimento e agenda, testando CI .NET e Postgres.
- Próximo alvo em `AtendimentoOrquestradorService.cs`: linhas **136–244** e **334–429** ainda não inspecionadas completamente.
- Decisão: não modificar o banco, não alterar o APK nem introduzir estilos adicionais sem teste real.

**Não está concluída** a auditoria semântica integral; a taxa de 100% se aplica apenas a catalogação e varredura automatizada.
