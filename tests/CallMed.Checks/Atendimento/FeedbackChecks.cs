using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Agente;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.ViewModels;

internal static class FeedbackChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        check(new[] { "pode encerrar", "Pode encerrar o atendimento!", "era só isso, obrigado", "tchau", "Até mais" }.All(AtendimentoFeedbackService.EhEncerramento),
            "Feedback reconhece encerramento explícito com acentos e pontuação");
        check(new[] { "obrigado", "5", "quero cancelar a consulta", "quero encerrar uma consulta", "tchau, quero saber horários", "era só isso? ainda quero agendar" }.All(s => !AtendimentoFeedbackService.EhEncerramento(s)),
            "Feedback não encerra agradecimento intermediário, opções de agenda ou cancelamentos");
        foreach (var canal in new[] { CanalAtendimento.Web, CanalAtendimento.WhatsApp, CanalAtendimento.Sms, CanalAtendimento.Email })
        {
            await using var f = await Fixture.Create(canal);
            var a = (await f.Feedback.EncerrarAsync(f.Conversa))!;
            var id = a.Id;
            await f.Feedback.EncerrarAsync(f.Conversa);
            check(!f.Conversa.Ativa && await f.Db.AvaliacoesAtendimento.CountAsync() == 1 && (await f.Feedback.ObterPendenteAsync(f.Conversa))?.Id == id,
                $"{canal}: encerrar duas vezes abre só uma avaliação");
            await f.Feedback.ProcessarEnviosAsync(); await f.Feedback.ProcessarEnviosAsync();
            check(f.Canal.Calls == (canal == CanalAtendimento.Web ? 0 : 1), $"{canal}: convite usa o canal original uma única vez; site usa cartão");
            var quantidade = await f.Db.MensagensAtendimento.CountAsync();
            var entrada = new CanalMensagemEntrada { Canal = canal, Identificador = f.Conversa.IdentificadorExterno,
                Texto = canal == CanalAtendimento.Email ? "NOTA 5 - Muito bom\n\nEm qua., 4 de nov. de 2026, CallMed <teste@example.test> escreveu:\n> texto antigo" : "NOTA 5 - Muito bom", MensagemExternaId = "teste:nota" };
            check(await f.Feedback.TratarEntradaAsync(entrada) && a.Nota == 5 && a.Comentario == "Muito bom" && !f.Conversa.Ativa &&
                quantidade == await f.Db.MensagensAtendimento.CountAsync(), $"{canal}: nota e comentário ficam separados do histórico e não reabrem a conversa");
            await f.Feedback.TratarEntradaAsync(entrada); await f.Feedback.ProcessarEnviosAsync(); await f.Feedback.ProcessarEnviosAsync();
            check(await f.Db.AvaliacoesAtendimento.CountAsync(x => x.Nota != null) == 1 && f.Canal.Calls == (canal == CanalAtendimento.Web ? 0 : 2),
                $"{canal}: reentrega da resposta não duplica nota nem agradecimento");
            check((await f.Conversas.CarregarHistoricoPacienteAsync(null, f.Conversa.Id)).All(m => !m.Texto.Contains("NOTA 5") && m.Texto != AtendimentoFeedbackService.Obrigado),
                $"{canal}: pesquisa não contamina o contexto da IA");
            await f.Conversas.ObterOuCriarAsync(canal, f.Conversa.IdentificadorExterno);
            check(!await f.Feedback.TratarEntradaAsync(new() { Canal = canal, Identificador = f.Conversa.IdentificadorExterno, Texto = "1" }) &&
                await f.Feedback.TratarEntradaAsync(entrada), $"{canal}: opções numéricas do novo atendimento ficam disponíveis, webhook antigo continua deduplicado");
            await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Nova pergunta", "teste:pergunta-2");
            await f.Feedback.EncerrarAsync(f.Conversa);
            check(await f.Db.AvaliacoesAtendimento.CountAsync() == 2 && (await f.Feedback.ObterPendenteAsync(f.Conversa))?.Id != id,
                $"{canal}: novo atendimento recebe nova avaliação");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp))
        {
            var a = (await f.Feedback.EncerrarAsync(f.Conversa))!;
            check(await f.Feedback.RegistrarAsync(a.Id, f.Conversa.Id, 0, null, false) == ResultadoAvaliacao.Invalida &&
                await f.Feedback.RegistrarAsync(a.Id, f.Conversa.Id, 6, null, false) == ResultadoAvaliacao.Invalida &&
                await f.Feedback.RegistrarAsync(a.Id, f.Conversa.Id, 4, new string('x', 1001), false) == ResultadoAvaliacao.Invalida,
                "Backend rejeita nota fora da escala e comentário acima do limite");
            check(!await f.Feedback.TratarEntradaAsync(new() { Canal = f.Conversa.Canal, Identificador = "551188887777", Texto = "5" }) && a.Nota is null,
                "Remetente diferente não avalia a conversa de outra pessoa");
            await f.Feedback.TratarEntradaAsync(new() { Canal = f.Conversa.Canal, Identificador = f.Conversa.IdentificadorExterno, Texto = "9" });
            await f.Feedback.ProcessarEnviosAsync();
            await f.Feedback.TratarEntradaAsync(new() { Canal = f.Conversa.Canal, Identificador = f.Conversa.IdentificadorExterno, Texto = "9" });
            await f.Feedback.ProcessarEnviosAsync();
            check(f.Canal.Calls == 2 && a.Nota is null && !f.Conversa.Ativa, "Nota inválida recebe orientação apenas uma vez, sem chamar a IA");
            check(await f.Feedback.RegistrarAsync(a.Id, f.Conversa.Id, 3, "  <script>exemplo</script>  ", false) == ResultadoAvaliacao.Registrada && a.Comentario == "<script>exemplo</script>" &&
                await f.Feedback.RegistrarAsync(a.Id, f.Conversa.Id, 1, "alterada", false) == ResultadoAvaliacao.JaRegistrada && a.Nota == 3,
                "Primeira resposta válida é preservada e comentário é armazenado como texto");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Sms))
        {
            var sms = new HttpSmsAtendimentoService(f.Db, f.Conversas, f.Orq, Options.Create(new SmsHttpOptions { Enabled = true }), NullLogger<HttpSmsAtendimentoService>.Instance, f.Feedback);
            await sms.RegistrarEntradaAsync(f.Conversa.IdentificadorExterno, "pode encerrar", "fechar", default);
            await sms.RegistrarEntradaAsync(f.Conversa.IdentificadorExterno, "pode encerrar", "fechar", default);
            await sms.RegistrarEntradaAsync(f.Conversa.IdentificadorExterno, "4 - Rápido", "avaliar", default);
            check(f.Canal.Calls == 0 && f.Agente.Calls == 0 && !f.Conversa.Ativa && await f.Db.AvaliacoesAtendimento.CountAsync(a => a.Nota == 4) == 1,
                "Webhook httpSMS persiste fechamento e nota sem esperar HTTP de saída nem IA");
            check(await sms.ProcessarPendenciasAsync() == 0, "Fila SMS não reprocessa feedback como atendimento");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email))
        {
            await f.Orq.ProcessarEntradaEmailComoRascunhoAsync(new() { Canal = CanalAtendimento.Email, Identificador = f.Conversa.IdentificadorExterno, Texto = "era só isso", MensagemExternaId = "gmail:fechar" });
            await f.Orq.ProcessarEntradaAsync(new() { Canal = CanalAtendimento.Email, Identificador = f.Conversa.IdentificadorExterno, Texto = "5", MensagemExternaId = "gmail:nota" });
            check(f.Agente.Calls == 0 && !f.Conversa.Ativa && await f.Db.AvaliacoesAtendimento.CountAsync(a => a.Nota == 5) == 1,
                "Ambas as entradas de e-mail capturam encerramento/feedback antes de gerar resposta ou reabrir conversa");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp))
        {
            await f.Orq.ProcessarEntradaAsync(new() { Canal = f.Conversa.Canal, Identificador = f.Conversa.IdentificadorExterno, Texto = "tchau", MensagemExternaId = "wa:fechar" });
            var a = (await f.Feedback.ObterPendenteAsync(f.Conversa))!;
            check(f.Agente.Calls == 0 && !f.Conversa.Ativa, "WhatsApp encerra por despedida explícita sem nova chamada ao modelo");
            await f.Feedback.RegistrarAsync(a.Id, f.Conversa.Id, null, null, true);
            await f.Feedback.ProcessarEnviosAsync();
            check(a.DispensadaEm != null && f.Canal.Calls == 0 && await f.Feedback.ObterPendenteAsync(f.Conversa) is null,
                "Dispensar avaliação é opcional e não dispara convite atrasado");
            await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Outra conversa");
            var nova = (await f.Feedback.EncerrarAsync(f.Conversa))!; nova.CriadoEm = DateTime.UtcNow.AddDays(-8); await f.Db.SaveChangesAsync();
            check(await f.Feedback.ObterPendenteAsync(f.Conversa) is null &&
                await f.Feedback.RegistrarAsync(nova.Id, f.Conversa.Id, 5, null, false) == ResultadoAvaliacao.Expirada, "Avaliação expira após sete dias");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp))
        {
            await f.Feedback.EncerrarAsync(f.Conversa); f.Canal.Falhar = true;
            await f.Feedback.ProcessarEnviosAsync(); await f.Feedback.ProcessarEnviosAsync();
            check(f.Canal.Calls == 1 && await f.Db.MensagensAtendimento.AnyAsync(m => m.Status == StatusMensagemAtendimento.Falhou && m.ChaveEnvio!.StartsWith("feedback:")),
                "Falha do convite fica visível na Central e não gera repetição automática infinita");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp))
        {
            var fechar = new CanalMensagemEntrada { Canal = f.Conversa.Canal, Identificador = f.Conversa.IdentificadorExterno, Texto = "tchau", MensagemExternaId = "wa:fechar-antigo" };
            await f.Feedback.TratarEntradaAsync(fechar);
            var repetida = new CanalMensagemEntrada { Canal = f.Conversa.Canal, Identificador = f.Conversa.IdentificadorExterno, Texto = "tchau", MensagemExternaId = "wa:fechar-repetido" };
            await f.Feedback.TratarEntradaAsync(repetida);
            await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Nova dúvida", "wa:nova");
            check(await f.Feedback.TratarEntradaAsync(repetida) && f.Conversa.Ativa,
                "Despedida repetida após encerramento também é deduplicada sem afetar o próximo ciclo");
            check(await f.Feedback.TratarEntradaAsync(fechar) && f.Conversa.Ativa,
                "Reentrega de encerramento antigo não fecha uma conversa reaberta");
            await f.Feedback.EncerrarAsync(f.Conversa); await f.Feedback.ProcessarEnviosAsync();
            check(f.Canal.Calls == 1 && await f.Db.AvaliacoesAtendimento.CountAsync(a => a.DispensadaEm == null) == 1,
                "Dois ciclos antes do processamento enviam somente o convite mais recente");
        }
        await CheckSiteAsync(check);
    }

    private static async Task CheckSiteAsync(Action<bool, string> check)
    {
        await using var f = await Fixture.Create(CanalAtendimento.Web);
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton(f.Db); services.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<MKSANContext>();
        using var provider = services.BuildServiceProvider();
        var vinculos = new UsuarioVinculoService(f.Db, provider.GetRequiredService<UserManager<Usuario>>());
        f.Db.Pacientes.Add(new Paciente { Id = 1, UsuarioId = "dono", Nome = "Teste", Cpf = "52998224725", Ativo = true });
        f.Db.Pacientes.Add(new Paciente { Id = 2, UsuarioId = "outro", Nome = "Outro", Cpf = "11144477735", Ativo = true });
        f.Db.Funcionarios.Add(new Funcionario { UsuarioId = "equipe", Nome = "Teste", Email = "equipe@example.test", Ativo = true });
        f.Conversa.IdentificadorExterno = "dono"; await f.Db.SaveChangesAsync();
        var controller = new AgenteController(f.Agente, vinculos, new AgenteHistoricoService(f.Db, NullLogger<AgenteHistoricoService>.Instance), new ConvenioService(new ClinicaClock()), f.Conversas, NullLogger<AgenteController>.Instance, f.Feedback);
        void Usuario(string id, string role = "Paciente") => controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal(id, role) } };
        Usuario("dono");
        check(await controller.Encerrar(default) is JsonResult && (await f.Feedback.ObterPendenteAsync(f.Conversa)) is not null, "Site: botão encerra e disponibiliza avaliação persistida");
        var a = (await f.Feedback.ObterPendenteAsync(f.Conversa))!;
        check(await controller.NovasMensagens(0) is JsonResult json && System.Text.Json.JsonSerializer.Serialize(json.Value).Contains(a.Id.ToString()),
            "Site: polling restaura avaliação pendente após recarregar a página");
        Usuario("outro");
        check(await controller.Avaliar(new() { Id = a.Id, Nota = 5 }, default) is NotFoundResult && a.Nota is null,
            "Site: outro usuário não pode avaliar por ID de avaliação alheia");
        Usuario("dono");
        check(await controller.Avaliar(new() { Id = a.Id, Nota = 6 }, default) is BadRequestObjectResult &&
            await controller.Avaliar(new() { Id = a.Id, Nota = 4, Comentario = "Site fácil" }, default) is JsonResult &&
            await controller.Avaliar(new() { Id = a.Id, Nota = 1 }, default) is JsonResult && a.Nota == 4,
            "Site: valida escala e torna envio repetido idempotente");
        var paciente = await f.Db.Pacientes.FindAsync(1); paciente!.Ativo = false; await f.Db.SaveChangesAsync();
        check(await controller.Encerrar(default) is ForbidResult && await controller.Avaliar(new() { Id = a.Id, Nota = 5 }, default) is ForbidResult,
            "Site: cadastro inativo não encerra nem avalia");
        check(typeof(AgenteController).GetMethod("Avaliar")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null &&
            typeof(AgenteController).GetMethod("Encerrar")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null &&
            typeof(AvaliacoesController).GetCustomAttribute<AuthorizeAttribute>()?.Roles == "Funcionario,Admin",
            "Avaliar/encerrar exigem antiforgery e relatório é restrito à equipe");
        var report = new AvaliacoesController(f.Db, vinculos, new ClinicaClock()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal("equipe", "Funcionario") } } };
        var view = (ViewResult)await report.Index(null, null);
        var model = (AvaliacoesViewModel)view.Model!;
        check(model.Respondidas == 1 && model.Media == 4 && model.Satisfacao == 100 && model.Itens.Single().Comentario == "Site fácil",
            "Relatório calcula média e satisfação somente sobre respostas recebidas");
    }
    private static ClaimsPrincipal Principal(string id, string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], "teste"));
    private sealed class Fixture : IAsyncDisposable
    {
        public required MKSANContext Db; public required ConversaAtendimento Conversa; public required AtendimentoConversaService Conversas;
        public required AtendimentoFeedbackService Feedback; public required AtendimentoOrquestradorService Orq; public required FakeCanal Canal; public required FakeAgente Agente;
        public static async Task<Fixture> Create(CanalAtendimento canal)
        {
            var db = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var conversa = new ConversaAtendimento { Canal = canal, IdentificadorExterno = canal == CanalAtendimento.Email ? "teste@example.test" : canal == CanalAtendimento.Web ? "dono" : "5511999990000", SessionId = "sessao", Modo = ModoAtendimento.Humano };
            db.Add(conversa); await db.SaveChangesAsync();
            var conversas = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db));
            await conversas.RegistrarEntradaAsync(conversa, "Olá", "teste:primeira");
            var sender = new FakeCanal { Canal = canal };
            var envio = new AtendimentoEnvioService([sender], db, TimeProvider.System, NullLogger<AtendimentoEnvioService>.Instance, Options.Create(new GmailInboxOptions()));
            var feedback = new AtendimentoFeedbackService(db, conversas, envio, TimeProvider.System);
            var agente = new FakeAgente();
            var orq = new AtendimentoOrquestradorService(agente, conversas, envio, null!, new ConvenioService(new ClinicaClock()), null!, NullLogger<AtendimentoOrquestradorService>.Instance, Options.Create(new AtendimentoEmailOptions()), feedback);
            return new() { Db = db, Conversa = conversa, Conversas = conversas, Feedback = feedback, Orq = orq, Canal = sender, Agente = agente };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class FakeCanal : ICanalAtendimentoSender
    {
        public CanalAtendimento Canal { get; init; } public bool Configurado => true; public int Calls; public bool Falhar;
        public Task<CanalEnvioResultado> EnviarAsync(string destinatario, string texto, string? assunto = null, CancellationToken ct = default, string? replyTo = null)
        { Calls++; return Task.FromResult(Falhar ? CanalEnvioResultado.Falha("Falha simulada") : CanalEnvioResultado.Ok("teste:" + Calls)); }
    }
    private sealed class FakeAgente : IAgenteClinicaService
    {
        public int Calls;
        public Task<AgenteResposta> EnviarAsync(string mensagem, string? sessionId, AgenteUsuarioContexto usuario, IReadOnlyList<MensagemHistoricoAgente>? historicoCliente = null, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("Feedback não deve chamar a IA"); }
    }
}
