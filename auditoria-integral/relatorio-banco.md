# Banco de dados — evidências e lacunas

- Contexto: `src/CallMed.Persistence/Data/MKSANContext.cs`, deriva de `IdentityDbContext<Usuario>` e implementa `IDataProtectionKeyContext`.
- Declarações `DbSet<...>` localizadas no contexto: **46** (incluem aliases; não correspondem necessariamente a tabelas únicas).
- Chamadas `HasIndex(...)` encontradas em arquivos C# da Persistência: **91**; isso não equivale a índices ativos em PostgreSQL.
- Migrations C# principais identificadas: **5**; arquivos de migração e snapshot constam no inventário.
- Regras verificadas por leitura contextual: `Oferta` possui índice único filtrado `Estado = 'Aberta'` para médico/data/horário; `Jornada` tem chave por consulta; `MKSANContext` registra `PepEvento` em `SaveChanges` quando consulta é adicionada/alterada.
- O `AgendamentoService.CriarAsync` utiliza transação e bloqueia o horário antes de verificar disponibilidade; `EncaixeService.ReservarAsync` usa advisory locks de PostgreSQL e valida o slot novamente no banco. São sinais de proteção, **não prova de concorrência correta em todos os cenários**.

## Pendente
Executar migration em banco isolado, validar divergência de snapshot e migrations, índices reais e concorrência sob duas sessões PostgreSQL. Não foi conectado banco real nem alterado produção.
