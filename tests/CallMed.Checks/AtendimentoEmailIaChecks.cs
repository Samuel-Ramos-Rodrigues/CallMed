using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

internal static class AtendimentoEmailIaChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        check(!AtendimentoEmailPolitica.ContemIdentificadorSensivel("QUE HORAS ABRE AÍ?"),
            "Perguntas comuns não sofrem bloqueio por listas fixas");
        check(AtendimentoEmailPolitica.ContemIdentificadorSensivel("Meu CPF é 529.982.247-25"),
            "Identificadores pessoais não são enviados para classificação");
        check(AtendimentoEmailDecisaoService.InterpretarResultado("{\"acao\":\"RASCUNHO\"}")
              == DecisaoEmail.PrepararRascunho,
            "Classificação semântica permite gerar rascunho");
        check(AtendimentoEmailDecisaoService.InterpretarResultado("{\"acao\":\"EQUIPE\"}")
              == DecisaoEmail.Equipe,
            "Classificação semântica respeita encaminhamento");
        check(AtendimentoEmailDecisaoService.InterpretarResultado("resposta inválida")
              == DecisaoEmail.Indisponivel,
            "Falha de interpretação não provoca envio nem rascunho indevido");

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

        // Uma nova pergunta depois de um rascunho antigo precisa voltar a ser
        // analisável, sem reprocessar e-mails anteriores.
        db.MensagensAtendimento.Add(new MensagemAtendimento
        {
            Id = 9999,
            ConversaAtendimentoId = 301,
            Autor = AutorMensagemAtendimento.Paciente,
            Direcao = DirecaoMensagemAtendimento.Entrada,
            Status = StatusMensagemAtendimento.Recebida,
            Texto = "Quais medicu tem ai"
        });
        await db.SaveChangesAsync();
        check((await service.ObterUltimaEntradaPendenteAsync(301))?.Id == 9999,
            "Nova mensagem após rascunho anterior permite nova análise da IA");
    }
}
