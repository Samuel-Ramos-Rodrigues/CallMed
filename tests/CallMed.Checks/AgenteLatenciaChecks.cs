using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

internal static class AgenteLatenciaChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var handler = new GeminiHandler();
        var options = Options.Create(new GeminiOptions { ApiKey = "teste-sem-rede", InteractiveTimeoutSeconds = 1 });
        var gemini = new GeminiClient(new HttpClient(handler), options, NullLogger<GeminiClient>.Instance);
        var agente = new AgenteClinicaService(gemini, null!, options, NullLogger<AgenteClinicaService>.Instance);
        var usuario = new AgenteUsuarioContexto { Email = "alex@example.test", PacienteId = 1, PacienteNome = "Alex Teste" };
        var sid = Guid.NewGuid().ToString();
        foreach (var saudacao in new[] { "Oi", "Olá!", "Bom dia", "Boa tarde", "Boa noite" })
        {
            var resposta = await agente.EnviarAsync(saudacao, sid, usuario);
            check(handler.Calls == 0 && resposta.Resposta.Contains("Alex") && resposta.SessionId == sid,
                $"Saudação '{saudacao}' responde sem Gemini e preserva a sessão");
        }
        await agente.EnviarAsync("Oi, quero marcar uma consulta", sid, usuario);
        check(handler.Calls == 1, "Saudação com pedido segue o agente completo e não é tratada como cumprimento isolado");

        // Um cancelamento libera a sessão para o próximo envio, inclusive uma saudação.
        handler.Suspender = true;
        using (var cancel = new CancellationTokenSource())
        {
            var pendente = agente.EnviarAsync("Quais médicos atendem?", sid, usuario, cancellationToken: cancel.Token);
            await handler.Iniciou.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancel.Cancel();
            try { await pendente; } catch (OperationCanceledException) { }
        }
        var recuperada = await agente.EnviarAsync("Oi", sid, usuario).WaitAsync(TimeSpan.FromSeconds(2));
        check(recuperada.Resposta.Contains("Alex"), "Cancelar uma geração libera a trava da sessão para a próxima mensagem");
        handler.Suspender = false;

        var saves = new SaveCounter();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddDbContext<MKSANContext>(o => o.UseInMemoryDatabase("latencia-" + Guid.NewGuid()).AddInterceptors(saves));
        services.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<MKSANContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MKSANContext>();
        db.Pacientes.Add(new Paciente { Id = 1, UsuarioId = "alex", Nome = "Alex Teste", Email = usuario.Email, Cpf = "52998224725", Ativo = true });
        var conversa = new ConversaAtendimento { Canal = CanalAtendimento.Web, IdentificadorExterno = "alex", PacienteId = 1, SessionId = "latencia-" + Guid.NewGuid() };
        db.Add(conversa); await db.SaveChangesAsync();
        var conversas = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db));
        var envio = new AtendimentoEnvioService([], db, TimeProvider.System, NullLogger<AtendimentoEnvioService>.Instance, Options.Create(new GmailInboxOptions()));
        var feedback = new AtendimentoFeedbackService(db, conversas, envio, TimeProvider.System);
        var historico = new AgenteHistoricoService(db, NullLogger<AgenteHistoricoService>.Instance);
        var vinculos = new UsuarioVinculoService(db, scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>());
        var controller = new AgenteController(agente, vinculos, historico, new ConvenioService(new ClinicaClock()), conversas,
            NullLogger<AgenteController>.Instance, feedback, options)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "alex"), new Claim(ClaimTypes.Name, usuario.Email!), new Claim(ClaimTypes.Role, "Paciente")], "teste")) } }
        };
        var requestsBefore = handler.Calls;
        var result = await controller.Enviar(new() { Mensagem = "Oi", SessionId = conversa.SessionId }, default);
        check(result is JsonResult && handler.Calls == requestsBefore, "Chat autenticado também usa a saudação rápida, com autorização e registro no banco");
        check(await db.MensagensAtendimento.CountAsync() == 2 && await db.MensagensAgente.CountAsync() == 0 &&
            (await conversas.CarregarHistoricoPacienteAsync(1, conversa.Id)).Count == 2,
            "Chat novo preserva o histórico na Central sem duplicá-lo no histórico V12");

        // Comparação do mesmo par usuário/resposta: fluxo anterior ainda disponível para migração.
        saves.Total = 0;
        await historico.SalvarInteracaoAsync("alex", "legado-" + Guid.NewGuid(), "Pergunta", "Resposta");
        await conversas.RegistrarEntradaAsync(conversa, "Pergunta");
        await conversas.RegistrarSaidaAsync(conversa, "Resposta", AutorMensagemAtendimento.Assistente, StatusMensagemAtendimento.Enviada);
        var anterior = saves.Total;
        saves.Total = 0;
        await conversas.RegistrarInteracaoWebAsync(conversa, "Pergunta rápida", "Resposta rápida");
        check(anterior == 4 && saves.Total == 1, "Persistência do par de mensagens cai de quatro gravações para uma, sem apagar o legado");
        var pair = await db.MensagensAtendimento.OrderByDescending(x => x.Id).Take(2).OrderBy(x => x.Id).ToListAsync();
        check(pair[0].Direcao == DirecaoMensagemAtendimento.Entrada && pair[1].Direcao == DirecaoMensagemAtendimento.Saida &&
            pair[0].CriadoEm < pair[1].CriadoEm && pair[1].Status == StatusMensagemAtendimento.Enviada,
            "Gravação em lote mantém ordem, autoria e estado das mensagens do site");

        handler.Suspender = true; handler.Iniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = Stopwatch.StartNew(); requestsBefore = handler.Calls;
        var expirou = await controller.Enviar(new() { Mensagem = "Quais especialidades atendem?", SessionId = conversa.SessionId }, default);
        check(expirou is ObjectResult { StatusCode: 504 } && handler.Calls == requestsBefore + 1 && timer.Elapsed < TimeSpan.FromSeconds(5),
            "Orçamento total interrompe Gemini lento e devolve HTTP 504 sem repetir a mensagem");
        handler.Suspender = false;
        var voltou = await controller.Enviar(new() { Mensagem = "Oi", SessionId = conversa.SessionId }, default);
        check(voltou is JsonResult, "Chat volta a responder depois de um timeout sem travar o atendimento");

        var paciente = await db.Pacientes.FindAsync(1); paciente!.Ativo = false; await db.SaveChangesAsync();
        check(await controller.Enviar(new() { Mensagem = "Oi" }, default) is ForbidResult && await controller.NovasMensagens() is ForbidResult,
            "Resposta rápida e polling continuam bloqueados para paciente inativo");

        // O filtro comum do widget é puro: deve funcionar mesmo sem acesso ao contexto.
        var semBanco = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("fechado").Options);
        var filtro = new AtendimentoFeedbackService(semBanco, null!, null!, TimeProvider.System);
        await semBanco.DisposeAsync();
        check(!await filtro.TratarEntradaAsync(new() { Canal = CanalAtendimento.Web, Identificador = "alex", Texto = "Quero consultar horários" }),
            "Mensagem comum do site não acessa o banco para procurar feedback");
    }
    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Total;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Total++; return ValueTask.FromResult(result); }
    }
    private sealed class GeminiHandler : HttpMessageHandler
    {
        public int Calls; public bool Suspender;
        public TaskCompletionSource Iniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; if (Suspender) { Iniciou.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Posso ajudar com o atendimento.\"}]}}]}", Encoding.UTF8, "application/json") };
        }
    }
}
