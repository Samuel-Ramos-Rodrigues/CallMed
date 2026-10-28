using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;
using MKSANCrud.Services.Clinica;

internal static class EvolutionEnvioChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var clock = new Clock();
        var options = Options.Create(new EvolutionWhatsAppOptions
        {
            Enabled = true, BaseUrl = "https://evolution.test", ApiKey = "test-only", InstanceName = "callmed",
            IntervaloMinimoEnvioMs = 100, EsperaApos429Segundos = 5
        });
        var control = new EvolutionEnvioControle(clock, options);
        var http = new Handler();
        http.Responses.Enqueue(() => Limited(new RetryConditionHeaderValue(TimeSpan.FromSeconds(120))));
        var sender = new EvolutionWhatsAppSender(new HttpClient(http), options, NullLogger<EvolutionWhatsAppSender>.Instance, control);
        var result = await sender.EnviarAsync("5579999990000", "Teste local");
        check(!result.Sucesso && result.ProximaTentativaEm == clock.Now.AddSeconds(120).UtcDateTime && http.Calls == 1,
            "429 respeita Retry-After em segundos e não usa fallback de payload");
        var other = new EvolutionWhatsAppSender(new HttpClient(http), options, NullLogger<EvolutionWhatsAppSender>.Instance, control);
        await other.EnviarAsync("5579999990001", "Outra conversa");
        check(http.Calls == 1, "Pausa após 429 é compartilhada entre clientes e destinatários");
        clock.Advance(120);
        check((await other.EnviarAsync("5579999990001", "Teste após espera")).Sucesso && http.Calls == 2,
            "Envio pode prosseguir depois da espera indicada pelo servidor");

        clock.Advance(1);
        var until = clock.Now.AddMinutes(10);
        http.Responses.Enqueue(() => Limited(new RetryConditionHeaderValue(until)));
        result = await sender.EnviarAsync("5579999990000", "Data HTTP");
        check(result.ProximaTentativaEm == until.UtcDateTime, "429 respeita Retry-After em formato de data HTTP");
        clock.Advance(601);
        http.Responses.Enqueue(() => Limited(null));
        result = await sender.EnviarAsync("5579999990000", "Sem cabeçalho");
        check(result.ProximaTentativaEm == clock.Now.AddSeconds(10).UtcDateTime,
            "429 sem cabeçalho usa espera progressiva configurável");
        clock.Advance(11);
        http.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") });
        result = await sender.EnviarAsync("5579999990000", "Chave inválida");
        check(!result.Sucesso && result.ProximaTentativaEm is null,
            "401 não entra em repetição automática nem troca de payload");
        clock.Advance(1);
        http.Responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("payload") });
        var calls = http.Calls;
        result = await sender.EnviarAsync("5579999990000", "Compatibilidade");
        check(result.Sucesso && http.Calls == calls + 2, "Fallback legado continua disponível para rejeição de payload");
        clock.Advance(1);
        http.Responses.Enqueue(Ok);
        http.Responses.Enqueue(() => Limited(null));
        result = await sender.EnviarAsync("5579999990000", new string('x', 4000));
        check(!result.Sucesso && result.ReenvioBloqueado && result.ProximaTentativaEm is null,
            "Envio parcial não repete automaticamente trechos já confirmados");

        var uncertain = new Handler();
        uncertain.Responses.Enqueue(() => throw new HttpRequestException("Resposta perdida no teste"));
        var unknown = new EvolutionWhatsAppSender(new HttpClient(uncertain), options, NullLogger<EvolutionWhatsAppSender>.Instance,
            new EvolutionEnvioControle(clock, options));
        check((await unknown.EnviarAsync("5579999990000", "Timeout simulado")).ReenvioBloqueado,
            "Resultado de transporte incerto pede conferência em vez de repetir a mensagem");
        var pacingHttp = new Handler();
        var paced = new EvolutionWhatsAppSender(new HttpClient(pacingHttp), options, NullLogger<EvolutionWhatsAppSender>.Instance,
            new EvolutionEnvioControle(TimeProvider.System, options));
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(paced.EnviarAsync("5579999990000", "A"), paced.EnviarAsync("5579999990001", "B"));
        check(sw.ElapsedMilliseconds >= 90 && pacingHttp.Calls == 2, "Envios concorrentes respeitam intervalo mínimo");

        var dbOptions = new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new MKSANContext(dbOptions);
        var conversation = new ConversaAtendimento { Canal = CanalAtendimento.WhatsApp, IdentificadorExterno = "5579999990000", SessionId = "teste", Ativa = true };
        db.ConversasAtendimento.Add(conversation); await db.SaveChangesAsync();
        var channel = new Channel();
        var service = Service(db, clock, channel);
        channel.Next = new CanalEnvioResultado { Erro = "HTTP 429", ProximaTentativaEm = clock.Now.AddMinutes(2).UtcDateTime };
        var message = await service.EnviarAvisoAsync(conversation, "Aviso", "teste:1");
        await service.EnviarAvisoAsync(conversation, "Aviso", "teste:1");
        await service.ReenviarAsync(message.Id);
        check(channel.Calls == 1 && await db.MensagensAtendimento.CountAsync() == 1,
            "Aviso automático e botão manual respeitam a pausa sem criar duplicatas");
        clock.Advance(121); channel.Next = CanalEnvioResultado.Ok("provider-id");
        await service.ReenviarAsync(message.Id);
        await service.EnviarAvisoAsync(conversation, "Texto atualizado", "teste:1");
        await service.ReenviarAsync(message.Id);
        check(channel.Calls == 2 && message.Status == StatusMensagemAtendimento.Enviada && await db.MensagensAtendimento.CountAsync() == 1,
            "Reenvio atualiza a mesma mensagem e avisos concluídos não são enviados outra vez");
        var legacy = new MensagemAtendimento { ConversaAtendimentoId = conversation.Id, Texto = "Aviso antigo", Direcao = DirecaoMensagemAtendimento.Saida,
            Autor = AutorMensagemAtendimento.Sistema, Status = StatusMensagemAtendimento.Falhou, Erro = "Evolution retornou HTTP 429." };
        db.MensagensAtendimento.Add(legacy); await db.SaveChangesAsync();
        var reused = await service.EnviarAvisoAsync(conversation, "Aviso antigo", "teste:legado");
        check(reused.Id == legacy.Id && await db.MensagensAtendimento.CountAsync() == 2,
            "Aviso 429 da versão anterior é reutilizado sem apagar o histórico");
        channel.Next = new CanalEnvioResultado { Erro = "Envio parcial", ReenvioBloqueado = true };
        var partial = await service.EnviarAsync(conversation, "Parcial", AutorMensagemAtendimento.Funcionario);
        calls = channel.Calls; await service.ReenviarAsync(partial.Id);
        check(channel.Calls == calls, "Servidor bloqueia reenvio de resultado parcial mesmo com POST direto");
        channel.Next = CanalEnvioResultado.Falha("Falha recuperável manualmente");
        var failed = await service.EnviarAsync(conversation, "Clique repetido", AutorMensagemAtendimento.Funcionario);
        channel.Next = CanalEnvioResultado.Ok();
        channel.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        calls = channel.Calls;
        var first = service.ReenviarAsync(failed.Id);
        await channel.Started.Task;
        await using (var db2 = new MKSANContext(dbOptions))
        {
            var second = await Service(db2, clock, channel).ReenviarAsync(failed.Id);
            check(second?.Status == StatusMensagemAtendimento.Processando && channel.Calls == calls + 1,
                "Duas requisições de reenvio não disparam simultaneamente a mesma mensagem");
        }
        channel.Hold.SetResult(); await first; channel.Hold = null;
        var entity = db.Model.FindEntityType(typeof(MensagemAtendimento))!;
        check(entity.FindProperty(nameof(MensagemAtendimento.VersaoEnvio))!.IsConcurrencyToken &&
            entity.GetIndexes().Any(i => i.IsUnique && i.Properties.SingleOrDefault()?.Name == nameof(MensagemAtendimento.ChaveEnvio)),
            "Modelo exige chave única de aviso e controle de concorrência persistente");

        // Uma requisição antiga não pode reutilizar a versão anterior após outro envio falhar.
        await using (var staleDb = new MKSANContext(dbOptions))
        {
            var stale = await staleDb.MensagensAtendimento.SingleAsync(m => m.Id == partial.Id);
            partial.VersaoEnvio = Guid.NewGuid(); await db.SaveChangesAsync();
            stale.Status = StatusMensagemAtendimento.Processando; stale.VersaoEnvio = Guid.NewGuid();
            var conflict = false;
            try { await staleDb.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { conflict = true; }
            check(conflict, "Versão persistente rejeita tentativa baseada em estado antigo da mensagem");
        }
        await using (var postgres = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>()
            .UseNpgsql("Host=localhost;Database=callmed_test;Username=test").Options))
        {
            var ddl = postgres.Database.GenerateCreateScript();
            check(ddl.Contains("IX_MensagensAtendimento_ChaveEnvio") && ddl.Contains("ProximaTentativaEm") && ddl.Contains("VersaoEnvio"),
                "Modelo PostgreSQL gera as colunas e o índice de controle de envio");
        }

        await CheckWaitlist(check, clock);
    }

    private static async Task CheckWaitlist(Action<bool, string> check, Clock clock)
    {
        await using var db = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var patient = new Paciente { Id = 1, Nome = "Paciente de teste", Cpf = "52998224725", Telefone = "5579999990000", CanalPreferido = "WhatsApp", Ativo = true };
        var specialty = new Especialidade { Id = 1, Nome = "Clínica geral", Ativo = true };
        var doctor = new Medico { Id = 1, Nome = "Médico de teste", Especialidade = "Clínica geral", EspecialidadeId = 1, Ativo = true };
        db.AddRange(patient, specialty, doctor);
        db.Disponibilidades.Add(new Disponibilidade { Id = 1, MedicoId = 1, Data = clock.Hoje.AddDays(1), Horario = "09:00", Ativo = true });
        var item = new ListaEspera { Id = 1, PacienteId = 1, MedicoId = 1, Ativa = true };
        db.ListasEspera.Add(item); await db.SaveChangesAsync();
        var channel = new Channel { Next = new CanalEnvioResultado { Erro = "HTTP 429", ProximaTentativaEm = clock.Now.AddMinutes(20).UtcDateTime } };
        var conv = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db));
        var eligible = new ConvenioElegibilidadeService(db, new ConvenioService(clock));
        var waitlist = new ListaEsperaService(db, clock, conv, Service(db, clock, channel), new EspecialidadeService(db), eligible,
            new SolicitacaoAtendimentoService(db, eligible, new AuditoriaService(db, new HttpContextAccessor(), NullLogger<AuditoriaService>.Instance)));
        await waitlist.ProcessarNotificacoesAsync();
        await waitlist.ProcessarNotificacoesAsync();
        check(item.NotificadoEm is null && channel.Calls == 1 && await db.MensagensAtendimento.CountAsync() == 1,
            "Lista de espera com 429 conserva um único aviso e não marca notificação como enviada");
        clock.Advance(1201); channel.Next = CanalEnvioResultado.Ok();
        var count = await waitlist.ProcessarNotificacoesAsync();
        await waitlist.ProcessarNotificacoesAsync();
        check(count == 1 && item.NotificadoEm is not null && item.UltimaDisponibilidadeId == 1 && channel.Calls == 2 && await db.MensagensAtendimento.CountAsync() == 1,
            "Lista de espera retoma o mesmo aviso após a pausa e registra sucesso uma única vez");
    }

    private static AtendimentoEnvioService Service(MKSANContext db, Clock clock, Channel channel) =>
        new([channel], db, clock, NullLogger<AtendimentoEnvioService>.Instance);
    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent("{\"key\":{\"id\":\"test\"}}") };
    private static HttpResponseMessage Limited(RetryConditionHeaderValue? delay)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
        response.Headers.RetryAfter = delay; return response;
    }
    private sealed class Clock : TimeProvider, IClinicaClock
    {
        public DateTimeOffset Now { get; private set; } = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(int seconds) => Now = Now.AddSeconds(seconds);
        public DateTime Agora => Now.UtcDateTime;
        public DateTime Hoje => Agora.Date;
        public DateTime ConverterUtc(DateTime utc) => utc;
        public DateTime ConverterParaUtc(DateTime local) => local;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public Queue<Func<HttpResponseMessage>> Responses { get; } = new();
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(Responses.Count > 0 ? Responses.Dequeue()() : Ok()); }
    }
    private sealed class Channel : ICanalAtendimentoSender
    {
        public CanalAtendimento Canal => CanalAtendimento.WhatsApp;
        public bool Configurado => true;
        public int Calls { get; private set; }
        public CanalEnvioResultado Next { get; set; } = CanalEnvioResultado.Ok();
        public TaskCompletionSource? Hold { get; set; }
        public TaskCompletionSource? Started { get; set; }
        public async Task<CanalEnvioResultado> EnviarAsync(string destinatario, string texto, string? assunto = null, CancellationToken ct = default)
        { Calls++; Started?.TrySetResult(); if (Hold is not null) await Hold.Task; return Next; }
    }
}
