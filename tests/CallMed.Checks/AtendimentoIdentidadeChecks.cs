using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

internal static class AtendimentoIdentidadeChecks
{
    private const string Email = "paciente@example.test";

    public static async Task RunAsync(Action<bool, string> check)
    {
        // O Gmail desliga a identificação automática: a regra precisa valer
        // tanto na criação como na reabertura de uma conversa existente.
        await using (var db = await CriarCenarioAsync(conversaExistente: true))
        {
            var service = CriarService(db);
            var conversa = await service.ObterOuCriarAsync(
                CanalAtendimento.Email, Email, resolverIdentidade: false);

            check(conversa.PacienteId is null && conversa.Paciente is null &&
                  conversa.SessionId == "sessao-anonima",
                "E-mail existente não vincula paciente automaticamente quando resolverIdentidade=false");
            check((await db.ConversasAtendimento.SingleAsync()).PacienteId is null,
                "Conversa existente permanece sem vínculo no banco de dados");
        }

        await using (var db = await CriarCenarioAsync(conversaExistente: false))
        {
            var conversa = await CriarService(db).ObterOuCriarAsync(
                CanalAtendimento.Email, Email, resolverIdentidade: false);
            check(conversa.PacienteId is null && conversa.Paciente is null,
                "E-mail novo também não vincula paciente automaticamente");
        }

        await using (var db = await CriarCenarioAsync(conversaExistente: true))
        {
            var conversa = await CriarService(db).ObterOuCriarAsync(
                CanalAtendimento.Email, Email, resolverIdentidade: true);
            check(conversa.PacienteId == 1 && conversa.Paciente is not null,
                "Identificação automática continua funcionando quando explicitamente habilitada");
        }

        await using (var db = await CriarCenarioAsync(conversaExistente: true))
        {
            var conversa = await CriarService(db).ObterOuCriarAsync(
                CanalAtendimento.Email, Email, pacienteId: 1, resolverIdentidade: false);
            check(conversa.PacienteId == 1,
                "Vínculo explícito de paciente permanece permitido com identificação automática desativada");
        }

        await using (var db = await CriarCenarioAsync(conversaExistente: true, vinculoExistente: true))
        {
            var conversa = await CriarService(db).ObterOuCriarAsync(
                CanalAtendimento.Email, Email, resolverIdentidade: false);
            check(conversa.PacienteId == 1,
                "Vínculo existente e já confirmado é preservado");
        }
    }

    private static AtendimentoConversaService CriarService(MKSANContext db) =>
        new(db, new AtendimentoIdentidadeService(db));

    private static async Task<MKSANContext> CriarCenarioAsync(
        bool conversaExistente,
        bool vinculoExistente = false)
    {
        var options = new DbContextOptionsBuilder<MKSANContext>()
            .UseInMemoryDatabase("atendimento-identidade-" + Guid.NewGuid())
            .Options;
        var db = new MKSANContext(options);
        db.Pacientes.Add(new Paciente
        {
            Id = 1,
            Nome = "Paciente de teste",
            Cpf = "52998224725",
            Email = Email,
            Ativo = true
        });

        if (conversaExistente)
        {
            db.ConversasAtendimento.Add(new ConversaAtendimento
            {
                Id = 11,
                Canal = CanalAtendimento.Email,
                IdentificadorExterno = Email,
                SessionId = "sessao-anonima",
                PacienteId = vinculoExistente ? 1 : null,
                Ativa = true,
                Modo = ModoAtendimento.IA
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // Simula outro ciclo do leitor Gmail, com contexto novo.
        return db;
    }
}
