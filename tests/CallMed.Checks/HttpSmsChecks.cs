using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MKSANCrud.Controllers;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Agente;
using MKSANCrud.Extensions;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.Sms;
using MKSANCrud.Services.Clinica;

internal static class HttpSmsChecks
{
    private const string Secret = "segredo-ficticio-httpsms-para-testes-locais-1234567890";
    private const string Chip = "+5511999990000";
    private static SmsHttpOptions OptionsSms(bool enabled = true) => new()
    { Enabled = enabled, ApiKey = "chave-ficticia", From = Chip, WebhookSecret = Secret };

    public static async Task RunAsync(Action<bool, string> check)
    {
        foreach (var enabled in new[] { false, true })
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MKSANContextConnection"] = "Host=localhost;Database=teste;Username=teste",
                ["Atendimento:Sms:Http:Enabled"] = enabled.ToString(),
                ["Atendimento:Sms:Http:ApiKey"] = "chave-ficticia", ["Atendimento:Sms:Http:From"] = Chip
            }).Build();
            var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(config);
            services.AddCallMedApplication(config, new TestEnvironment());
            using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
            var sms = scope.ServiceProvider.GetServices<ICanalAtendimentoSender>().Where(s => s.Canal == CanalAtendimento.Sms).ToList();
            check(sms.Count == 1 && sms[0] is SmsHttpSender && sms[0].Configurado == enabled,
                enabled ? "DI registra somente httpSMS com API Key e From" : "httpSMS não envia sem habilitação");
            check(scope.ServiceProvider.GetService<HttpSmsAtendimentoService>() is not null, "DI registra o processamento persistido do httpSMS");
        }
        var http = new Handler(); var options = Options.Create(OptionsSms());
        var sender = new SmsHttpSender(new HttpClient(http), options, NullLogger<SmsHttpSender>.Instance);
        var result = await sender.EnviarAsync("(11) 98765-4321", " Olá ");
        using (var json = JsonDocument.Parse(http.Body!))
            check(result.Sucesso && result.MensagemExternaId == "http-envio-1" && result.StatusConfirmado == StatusMensagemAtendimento.AguardandoEnvio &&
                http.Url == "https://api.httpsms.com/v1/messages/send" && http.ApiKey == "chave-ficticia" && !http.BasicAuth &&
                json.RootElement.GetProperty("from").GetString() == Chip && json.RootElement.GetProperty("to").GetString() == "+5511987654321" &&
                json.RootElement.GetProperty("content").GetString() == "Olá", "httpSMS usa x-api-key, From/To/Content e mantém aceitação como fila");
        var calls = http.Calls;
        await sender.EnviarAsync("123", "Teste"); await sender.EnviarAsync("5511987654321", " ");
        check(http.Calls == calls, "httpSMS rejeita telefone inválido e texto vazio antes do HTTP");
        var old = new SmsHttpOptions { Enabled = true };
        check(!new SmsHttpSender(new HttpClient(http), Options.Create(old), NullLogger<SmsHttpSender>.Instance).Configurado,
            "Configuração sem API Key/From não reaproveita credenciais do SMSGate");
        var unsafeUrl = OptionsSms(); unsafeUrl.BaseUrl = "https://api.httpsms.com/v1?key=nao";
        check(!new SmsHttpSender(new HttpClient(http), Options.Create(unsafeUrl), NullLogger<SmsHttpSender>.Instance).Configurado,
            "httpSMS rejeita URL com parâmetros e credenciais embutidas");
        http.Status = HttpStatusCode.Unauthorized;
        check(!(await sender.EnviarAsync("5511987654321", "Teste")).Sucesso, "httpSMS não anuncia sucesso com chave recusada");
        http.Status = HttpStatusCode.TooManyRequests;
        check((await sender.EnviarAsync("5511987654321", "Teste")).ProximaTentativaEm is not null, "Limite do httpSMS preserva horário para nova tentativa");
        http.Status = HttpStatusCode.OK; http.ResponseBody = "{\"status\":\"success\"}";
        check((await sender.EnviarAsync("5511987654321", "Teste")).ReenvioBloqueado, "Aceitação sem ID não vira sucesso nem libera duplicação cega");
        http.ResponseBody = "{\"status\":\"error\",\"data\":{\"id\":\"nao-confirmado\"}}";
        check(!(await sender.EnviarAsync("5511987654321", "Teste")).Sucesso, "Envelope de erro não vira sucesso apenas por retornar HTTP 200");
        http.ResponseBody = null; http.NetworkError = true;
        check((await sender.EnviarAsync("5511987654321", "Teste")).ReenvioBloqueado, "Falha de rede sem confirmação bloqueia reenvio cego");
        http.NetworkError = false;

        check(HttpSmsWebhookValidator.Validar("Bearer " + Token(), Secret), "JWT HS256 do httpSMS é aceito");
        check(!HttpSmsWebhookValidator.Validar("Bearer " + Token(key: Secret + "outro"), Secret), "JWT assinado com outra chave é rejeitado");
        check(!HttpSmsWebhookValidator.Validar("Bearer " + Token(expired: true), Secret), "JWT expirado é rejeitado");
        check(!HttpSmsWebhookValidator.Validar("Bearer " + Token(future: true), Secret), "JWT ainda não válido é rejeitado");
        check(!HttpSmsWebhookValidator.Validar("Bearer " + Token(algorithm: SecurityAlgorithms.HmacSha384), Secret), "JWT com algoritmo diferente de HS256 é rejeitado");
        check(!HttpSmsWebhookValidator.Validar("Bearer invalido", Secret) && !HttpSmsWebhookValidator.Validar("", Secret), "JWT ausente ou malformado é rejeitado");

        var dbOptions = new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MKSANContext(dbOptions);
        var agent = new Agent(); var setup = Build(db, sender, agent, options);
        AtendimentoWebhookController Controller(string payload, bool signed = true, bool enabled = true, string? headerType = null)
        {
            var ctx = new DefaultHttpContext(); ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            if (signed) ctx.Request.Headers.Authorization = "Bearer " + Token();
            if (headerType is not null) ctx.Request.Headers["X-Event-Type"] = headerType;
            return new AtendimentoWebhookController(setup.Orquestrador, setup.Envio, Options.Create(new EvolutionWhatsAppOptions()),
                Options.Create(OptionsSms(enabled)), Options.Create(new GmailInboxOptions()), NullLogger<AtendimentoWebhookController>.Instance)
                { ControllerContext = new ControllerContext { HttpContext = ctx } };
        }
        var payload = Incoming("entrada-1", "Olá, quais especialidades atendem?");
        check(await Controller(payload, signed: false).Sms(setup.Service, default) is UnauthorizedResult && agent.Calls == 0,
            "Webhook httpSMS não chama IA com autenticação ausente");
        var legado = Controller(payload, signed: false); legado.Request.QueryString = new QueryString("?secret=" + Secret);
        legado.Request.Headers["X-Signature"] = "legado";
        check(await legado.Sms(setup.Service, default) is UnauthorizedResult, "Segredo na URL e headers antigos não substituem JWT");
        check(await Controller(payload, enabled: false).Sms(setup.Service, default) is NotFoundResult, "Webhook httpSMS desativado retorna 404");
        check(await Controller("{invalido").Sms(setup.Service, default) is BadRequestResult &&
            await Controller("[]").Sms(setup.Service, default) is BadRequestResult, "Webhook rejeita JSON inválido ou raiz sem objeto");
        check(await Controller(payload, headerType: "message.phone.sent").Sms(setup.Service, default) is BadRequestResult,
            "Webhook rejeita evento diferente entre corpo e cabeçalho");
        check(await Controller(Incoming("outro-chip", "Olá", owner: "+5511888880000")).Sms(setup.Service, default) is OkResult && !await db.MensagensAtendimento.AnyAsync(),
            "Evento de outro chip não abre conversa");
        check(await Controller("{\"type\":\"phone.heartbeat.online\"}").Sms(setup.Service, default) is OkResult,
            "Heartbeat não inicia atendimento nem chama IA");
        check(await Controller(payload).Sms(setup.Service, default) is OkResult && await Controller(payload).Sms(setup.Service, default) is OkResult &&
            agent.Calls == 0 && await db.MensagensAtendimento.CountAsync() == 1,
            "Webhook responde após persistir, sem aguardar IA, e não duplica o mesmo SMS");
        var conversa = await db.ConversasAtendimento.SingleAsync();
        var state = await setup.Conversas.ObterEstadoIaAsync(conversa, default);
        check(state.Estado == "pendente" && state.Detalhe.Contains("automaticamente"), "Central informa que a IA retomará o SMS automaticamente");
        // Nova instância de DbContext representa a retomada após reinício do processo.
        await using (var restarted = new MKSANContext(dbOptions))
        {
            var afterRestart = Build(restarted, sender, agent, options);
            check(await afterRestart.Service.ProcessarPendenciasAsync() == 1 && agent.Calls == 1,
                "Fila persistida do httpSMS sobrevive à recriação do contexto e gera uma resposta");
            check(await afterRestart.Service.ProcessarPendenciasAsync() == 0 && agent.Calls == 1,
                "Novo ciclo da fila não repete SMS já respondido");
        }
        db.ChangeTracker.Clear(); conversa = await db.ConversasAtendimento.SingleAsync();
        var saida = await db.MensagensAtendimento.SingleAsync(m => m.Direcao == DirecaoMensagemAtendimento.Saida);
        check(saida.Status == StatusMensagemAtendimento.AguardandoEnvio, "API aceitar não é exibido como SMS enviado ou entregue");
        check(await Controller(Event("message.phone.sent", saida.MensagemExternaId!)).Sms(setup.Service, default) is OkResult && saida.Status == StatusMensagemAtendimento.Enviada,
            "Callback sent confirma envio pelo aplicativo");
        await Controller(Event("message.phone.delivered", saida.MensagemExternaId!)).Sms(setup.Service, default);
        await Controller(Event("message.send.failed", saida.MensagemExternaId!)).Sms(setup.Service, default);
        check(saida.Status == StatusMensagemAtendimento.Entregue && agent.Calls == 1, "Entrega confirmada não regride com falha atrasada nem dispara IA");
        check(await Controller(Event("message.phone.sent", "ainda-nao-persistido")).Sms(setup.Service, default) is StatusCodeResult { StatusCode: 503 },
            "Callback que antecede a persistência do ID pede nova tentativa ao provedor");
        check(await Controller(Event("message.phone.sent", "teste-feito-no-painel", callMed: false)).Sms(setup.Service, default) is OkResult,
            "SMS enviado fora do CallMed é ignorado sem provocar falha no webhook");

        var falha = await setup.Envio.EnviarAsync(conversa, "Teste de retorno", AutorMensagemAtendimento.Funcionario);
        await Controller(Event("message.send.expired", falha.MensagemExternaId!, final: false)).Sms(setup.Service, default);
        check(falha.Status == StatusMensagemAtendimento.AguardandoEnvio, "Expiração provisória não cancela as tentativas do próprio httpSMS");
        await Controller(Event("message.send.failed", falha.MensagemExternaId!)).Sms(setup.Service, default);
        check(falha.Status == StatusMensagemAtendimento.Falhou && falha.Erro!.Contains("aplicativo"), "Falha do aplicativo aparece no histórico sem expor payload privado");
        conversa.Modo = ModoAtendimento.Humano; await db.SaveChangesAsync(); var agentCalls = agent.Calls;
        await Controller(Incoming("entrada-humana", "Quais os horários?")).Sms(setup.Service, default);
        await setup.Service.ProcessarPendenciasAsync();
        check(agent.Calls == agentCalls && await db.MensagensAtendimento.AnyAsync(m => m.MensagemExternaId == "httpsms:entrada-humana"),
            "Recebimento preserva mensagem sem responder durante atendimento humano");
        conversa.Modo = ModoAtendimento.IA; await db.SaveChangesAsync();
        await Controller(Incoming("pedido-humano", "Quero falar com um atendente")).Sms(setup.Service, default);
        await setup.Service.ProcessarPendenciasAsync();
        check(conversa.Modo == ModoAtendimento.Humano && agent.Calls == agentCalls &&
            await db.MensagensAtendimento.AnyAsync(m => m.ChaveEnvio != null && m.ChaveEnvio.StartsWith("sms:equipe:")),
            "Pedido de atendente pausa IA e envia confirmação pela mesma conversa");

        conversa.Modo = ModoAtendimento.IA; await db.SaveChangesAsync();
        await Controller(Incoming("agendamento", "Quero marcar uma consulta")).Sms(setup.Service, default);
        await setup.Service.ProcessarPendenciasAsync();
        check(await db.SolicitacoesAtendimento.AnyAsync(x => x.ConversaAtendimentoId == conversa.Id),
            "SMS de agendamento continua criando solicitação no fluxo existente");
        var status = JsonSerializer.Serialize(((OkObjectResult)Controller(payload).Status()).Value);
        check(status.Contains("httpSMS") && !status.Contains(Secret) && !status.Contains("chave-ficticia"),
            "Painel identifica httpSMS sem expor API Key ou Signing Key");

        var callsBefore = agent.Calls;
        var oldConversation = await setup.Conversas.ObterOuCriarAsync(CanalAtendimento.Sms, "+5511977771111");
        await setup.Conversas.RegistrarEntradaAsync(oldConversation, "Histórico antigo", "antigo-smsgate", default);
        await setup.Service.ProcessarPendenciasAsync();
        check(agent.Calls == callsBefore, "Troca de provedor não reprocessa histórico do SMSGate");

        // Uma tomada humana durante a geração impede a resposta automática.
        conversa.Modo = ModoAtendimento.IA; await db.SaveChangesAsync();
        await Controller(Incoming("tomada-durante-ia", "Quem atende hoje?")).Sms(setup.Service, default);
        agent.During = async () => { conversa.Modo = ModoAtendimento.Humano; await db.SaveChangesAsync(); };
        var sendsBefore = http.Calls; await setup.Service.ProcessarPendenciasAsync();
        check(http.Calls == sendsBefore, "Tomada humana durante geração impede envio posterior da IA");
        agent.During = null;
    }

    private static string Token(string? key = null, bool expired = false, bool future = false, string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(notBefore: now.AddMinutes(expired ? -20 : future ? 10 : -1),
            expires: now.AddMinutes(expired ? -10 : 20),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Secret)), algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private static string Incoming(string id, string content, string owner = Chip) => JsonSerializer.Serialize(new
    { type = "message.phone.received", id = "evento-" + id, data = new { message_id = id, owner, contact = "+5511987654321", content } });
    private static string Event(string type, string id, bool final = true, bool callMed = true) => JsonSerializer.Serialize(new
    { type, id = "evento-" + id, data = new { id, owner = Chip, contact = "+5511987654321", is_final = final, request_id = callMed ? "callmed:teste" : null } });
    private static (HttpSmsAtendimentoService Service, AtendimentoOrquestradorService Orquestrador, AtendimentoEnvioService Envio, AtendimentoConversaService Conversas)
        Build(MKSANContext db, SmsHttpSender sender, Agent agent, IOptions<SmsHttpOptions> options)
    {
        var conversas = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db), Options.Create(new AtendimentoEmailOptions()));
        var envio = new AtendimentoEnvioService([sender], db, TimeProvider.System, NullLogger<AtendimentoEnvioService>.Instance, Options.Create(new GmailInboxOptions()));
        var audit = new AuditoriaService(db, new HttpContextAccessor(), NullLogger<AuditoriaService>.Instance);
        var orq = new AtendimentoOrquestradorService(agent, conversas, envio, null!, null!, new SolicitacaoAtendimentoService(db, audit),
            NullLogger<AtendimentoOrquestradorService>.Instance, Options.Create(new AtendimentoEmailOptions()));
        return (new HttpSmsAtendimentoService(db, conversas, orq, options, NullLogger<HttpSmsAtendimentoService>.Instance), orq, envio, conversas);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public string? Url, Body, ApiKey, ResponseBody; public bool BasicAuth, NetworkError;
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(ct);
            ApiKey = request.Headers.GetValues("x-api-key").Single(); BasicAuth = request.Headers.Authorization is not null;
            if (NetworkError) throw new HttpRequestException("erro fictício de rede");
            return new(Status) { Content = new StringContent(ResponseBody ?? JsonSerializer.Serialize(new
                { status = "success", data = new { id = "http-envio-" + Calls, status = "pending" } }), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Agent : IAgenteClinicaService
    {
        public int Calls; public Func<Task>? During;
        public async Task<AgenteResposta> EnviarAsync(string mensagem, string? sessionId, AgenteUsuarioContexto usuario,
            IReadOnlyList<MensagemHistoricoAgente>? historico = null, CancellationToken ct = default)
        {
            Calls++; if (During is not null) await During();
            return new AgenteResposta { SessionId = sessionId ?? "teste", Resposta = "A clínica atende diversas especialidades." };
        }
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Checks"; public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory(); public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
