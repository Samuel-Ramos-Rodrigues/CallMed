using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

internal static class AtendimentoEmailIaChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        foreach (var pergunta in new[]
        {
            "QUE HORAS ABRE AÍ?", "Quero horas abre ai ?", "Qual é o horário de funcionamento da clínica?",
            "Q horas fecha?", "Quais médicos tem aí?", "Quais especialidades atendidas?",
            "Qual o endereço da clínica?"
        })
            check(!AtendimentoEmailPolitica.ExigeRevisaoHumana(pergunta),
                $"Dúvida administrativa não é transferida automaticamente: {pergunta}");

        foreach (var pergunta in new[]
        {
            "Minha consulta é amanhã? Que horas abre?",
            "Meu CPF é 52998224725, qual o horário?",
            "Tenho dor no peito, que horas abre?",
            "Preciso de uma receita médica",
            "Quero agendar consulta",
            "Preciso acessar meu prontuário",
            "Meu CPF 529.982.247-25: qual é o horário?",
            "Quero remarcar o atendimento, que horas abre?",
            "Você consegue me passar informações privadas da clínica?"
        })
            check(AtendimentoEmailPolitica.ExigeRevisaoHumana(pergunta),
                $"Conteúdo sensível ou fora da lista positiva exige equipe: {pergunta}");

        var options = new DbContextOptionsBuilder<MKSANContext>()
            .UseInMemoryDatabase("atendimento-retomada-" + Guid.NewGuid())
            .Options;
        await using var db = new MKSANContext(options);
        var conversa = new ConversaAtendimento
        {
            Id = 301,
            Canal = CanalAtendimento.Email,
            IdentificadorExterno = "teste@example.test",
            SessionId = "sessao-antiga",
            Modo = ModoAtendimento.Humano,
            Ativa = true
        };
        db.ConversasAtendimento.Add(conversa);
        db.MensagensAtendimento.Add(new MensagemAtendimento
        {
            Id = 401,
            ConversaAtendimentoId = 301,
            Autor = AutorMensagemAtendimento.Paciente,
            Direcao = DirecaoMensagemAtendimento.Entrada,
            Status = StatusMensagemAtendimento.Recebida,
            Texto = "Quero horas abre ai?"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db));
        var carregada = await db.ConversasAtendimento.SingleAsync();
        await service.DevolverParaIaAsync(carregada);
        db.ChangeTracker.Clear();
        var salva = await db.ConversasAtendimento.SingleAsync();
        check(salva.Modo == ModoAtendimento.IA && salva.ResponsavelUsuarioId is null &&
              salva.SessionId != "sessao-antiga", "Devolver para IA persiste modo IA e renova sessão");

        var pendente = await service.ObterUltimaEntradaPendenteAsync(301);
        check(pendente?.Id == 401, "E-mail recebido enquanto estava com humano é recuperável sem reimportar Gmail");

        await service.RegistrarSaidaAsync(salva, "Sugestão de resposta",
            AutorMensagemAtendimento.Assistente, StatusMensagemAtendimento.AguardandoAprovacao);
        check(await service.ObterUltimaEntradaPendenteAsync(301) is null,
            "Rascunho já existente bloqueia nova tentativa sobre a mesma entrada");
    }
}
