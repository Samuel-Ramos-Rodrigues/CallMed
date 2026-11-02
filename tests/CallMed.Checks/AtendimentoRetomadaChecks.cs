using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
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

internal static class AtendimentoRetomadaChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var texto = "Oi, preciso de médico do coração\n\nEm sáb., 31 de out. de 2026, 23:56, CallMed\n<recepcao@example.test> escreveu:\nMeu CPF é 529.982.247-25\n> texto antigo";
        check(AtendimentoEmailTexto.Normalizar(texto) == "Oi, preciso de médico do coração",
            "A mensagem atual é separada de uma citação Gmail com cabeçalho quebrado");
        check(AtendimentoEmailTexto.Normalizar("Preciso de cardiologia\n\nOn Sat, Oct 31, 2026\nReception <test@example.test> wrote:\nold") == "Preciso de cardiologia",
            "Citações em inglês também não contaminam a nova mensagem");
        check(AtendimentoEmailTexto.Normalizar("Em cardiologia, quais horários há?\nGostaria de informações.").Contains("Gostaria"),
            "Uma frase comum iniciada por Em não é removida como cabeçalho");
        check(AtendimentoEmailTexto.Normalizar("> Texto citado\nQuais médicos atendem?")=="Quais médicos atendem?",
            "Texto novo após uma linha citada continua disponível para análise");
        check(!AtendimentoEmailPolitica.ContemIdentificadorSensivel("Meu telefone é 11987654321") &&
            AtendimentoEmailPolitica.ContemIdentificadorSensivel("Meu CPF é 52998224725"),
            "Telefone não provoca bloqueio por CPF; CPF continua protegido");

        await using (var f = await Fixture.Create(CanalAtendimento.Email, texto))
        {
            f.Conversa.PacienteId = 1; // Mesmo um vínculo manual não autentica o remetente do e-mail.
            await f.Db.SaveChangesAsync();
            var controller = f.Controller();
            await controller.DevolverParaIa(f.Conversa.Id, default);
            var draft = await f.Db.MensagensAtendimento.SingleAsync(x => x.Direcao == DirecaoMensagemAtendimento.Saida);
            check(f.Agente.Calls == 1 && draft.Status == StatusMensagemAtendimento.AguardandoAprovacao && f.Canal.Calls == 0,
                "Um único POST DevolverParaIa gera o rascunho de e-mail sem enviar ao paciente");
            check(f.Agente.Texto == "Oi, preciso de médico do coração" && f.Agente.Usuario?.PacienteId is null &&
                f.Agente.Historico?.All(x => x.Texto != f.Entrada.Texto) == true,
                "Retomada usa a mensagem atual uma única vez e não autentica o remetente por e-mail");
            check(f.Gemini.Texto?.Contains("529") == false && f.Gemini.Texto == "Oi, preciso de médico do coração",
                "O classificador recebe apenas o texto atual, sem CPF citado no histórico antigo");
            await controller.DevolverParaIa(f.Conversa.Id, default);
            check(f.Agente.Calls == 1 && await f.Db.MensagensAtendimento.CountAsync() == 2,
                "Devolver novamente não duplica o rascunho nem reinsere a mensagem recebida");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Preciso de cardiologista. Meu telefone é 11987654321"))
        {
            var resultado=await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(resultado==ResultadoRetomadaAtendimento.RascunhoCriado && !f.Gemini.Texto!.Contains("11987654321") && !f.Agente.Texto!.Contains("11987654321"),
                "Telefone é omitido antes da IA, sem bloquear o rascunho administrativo");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Primeiro pedido"))
        {
            f.Conversa.Modo = ModoAtendimento.IA;
            await f.Db.SaveChangesAsync();
            var entrada = new CanalMensagemEntrada { Canal=CanalAtendimento.Email, Identificador=f.Conversa.IdentificadorExterno, Texto="Quais horários têm?", MensagemExternaId="gmail:teste-novo" };
            await f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(entrada);
            await f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(entrada);
            check(f.Agente.Calls==1 && f.Canal.Calls==0 && await f.Db.MensagensAtendimento.CountAsync(x=>x.Status==StatusMensagemAtendimento.AguardandoAprovacao)==1,
                "E-mail novo no modo IA cria rascunho automaticamente e a releitura Gmail não duplica");
            var tools = new AgenteToolsService(f.Db,null!,new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),null!,null!,null!,null!,null!,null!,null!,NullLogger<AgenteToolsService>.Instance);
            var usuario = new AgenteUsuarioContexto { Canal="E-mail",PacienteId=1,PodeGerenciarOutrosPacientes=true };
            var antes=await f.Db.Consultas.CountAsync();
            var bloqueados=new List<System.Text.Json.Nodes.JsonObject>();
            foreach(var nome in new[]{"agendar_consulta","cancelar_consulta","identificar_paciente","cadastrar_paciente"})
                bloqueados.Add(await tools.ExecutarAsync(nome,new(),"Confirmo",usuario,default));
            check(bloqueados.All(x=>x["sucesso"]?.GetValue<bool>()==false) && await f.Db.Consultas.CountAsync()==antes,
                "Rascunho de e-mail não pode usar ferramentas privadas ou alterar cadastros e agenda");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp, "Quais médicos têm aí?"))
        {
            await f.Controller().DevolverParaIa(f.Conversa.Id, default);
            var saida = await f.Db.MensagensAtendimento.SingleAsync(x => x.Direcao == DirecaoMensagemAtendimento.Saida);
            check(f.Agente.Calls == 1 && f.Canal.Calls == 1 && saida.Autor == AutorMensagemAtendimento.Assistente && saida.Status == StatusMensagemAtendimento.Enviada,
                "Um único POST DevolverParaIa responde à pendência no WhatsApp");
            check(f.Agente.Usuario?.PacienteId == 1 && f.Agente.Historico?.Count == 0 && f.Conversa.Modo == ModoAtendimento.IA,
                "A retomada WhatsApp recupera o vínculo existente e evita duplicar a entrada no contexto");
            await f.Controller().DevolverParaIa(f.Conversa.Id, default);
            check(f.Canal.Calls == 1 && f.Agente.Calls == 1, "Uma mensagem WhatsApp já respondida não é respondida novamente");
            await f.Orquestrador.ProcessarEntradaAsync(new CanalMensagemEntrada { Canal = CanalAtendimento.WhatsApp, Identificador = f.Conversa.IdentificadorExterno, Texto = "Quais os horários?", MensagemExternaId = "teste:segunda" });
            check(f.Canal.Calls == 2 && f.Agente.Calls == 2, "A próxima mensagem WhatsApp segue automaticamente na IA");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Preciso de cardiologista"))
        {
            await Task.WhenAll(f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa), f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa));
            check(f.Agente.Calls == 1 && await f.Db.MensagensAtendimento.CountAsync() == 2,
                "Duas retomadas simultâneas não geram dois rascunhos");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Meu CPF é 52998224725"))
        {
            var resultado = await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(resultado == ResultadoRetomadaAtendimento.RequerEquipe && f.Agente.Calls == 0 && f.Gemini.Calls == 0 && f.Canal.Calls == 0,
                "A retomada não contorna a proteção de identificadores pessoais");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp, "Preciso de cardiologista"))
        {
            f.Canal.Falhar = true;
            var resultado = await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(resultado == ResultadoRetomadaAtendimento.FalhaNoEnvio &&
                (await f.Db.MensagensAtendimento.SingleAsync(x => x.Direcao == DirecaoMensagemAtendimento.Saida)).Status == StatusMensagemAtendimento.Falhou,
                "Falha de transporte é registrada e não é anunciada como resposta enviada");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp, "Pergunta antiga"))
        {
            await f.Conversas.RegistrarSaidaAsync(f.Conversa,"Posso ajudar com informações sobre cardiologia e horários.",AutorMensagemAtendimento.Assistente,StatusMensagemAtendimento.Enviada);
            f.Db.MensagensAtendimento.Add(new MensagemAtendimento { Id=3,ConversaAtendimentoId=1,Direcao=DirecaoMensagemAtendimento.Entrada,Autor=AutorMensagemAtendimento.Paciente,Texto="Nova pergunta",Status=StatusMensagemAtendimento.Recebida });
            await f.Db.SaveChangesAsync();
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Canal.Calls==1 && await f.Db.MensagensAtendimento.CountAsync(x=>x.Direcao==DirecaoMensagemAtendimento.Saida)==2,
                "Resposta de mesmo texto a uma nova entrada não é confundida com aviso antigo já enviado");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp, "Preciso de cardiologista"))
        {
            await f.Conversas.RegistrarSaidaAsync(f.Conversa,"Sua conversa foi encaminhada para a equipe.",AutorMensagemAtendimento.Sistema,StatusMensagemAtendimento.Enviada);
            var resultado=await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(resultado==ResultadoRetomadaAtendimento.RespostaEnviada && f.Canal.Calls==1,
                "Aviso automático de transferência para equipe não impede a IA de retomar a pergunta no WhatsApp");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp, "Quero falar com um atendente"))
        {
            var resultado=await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(resultado==ResultadoRetomadaAtendimento.RequerEquipe && f.Conversa.Modo==ModoAtendimento.Humano && f.Canal.Calls==0,
                "Pedido explícito de atendimento humano continua sendo respeitado na retomada WhatsApp");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.WhatsApp, "Quais médicos?"))
        {
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, "A equipe já respondeu.", AutorMensagemAtendimento.Funcionario, StatusMensagemAtendimento.Enviada);
            check(await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa) == ResultadoRetomadaAtendimento.SemMensagemPendente && f.Agente.Calls == 0,
                "Resposta humana posterior impede que a IA repita o atendimento antigo");
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required MKSANContext Db;
        public required ConversaAtendimento Conversa;
        public required MensagemAtendimento Entrada;
        public required AtendimentoConversaService Conversas;
        public required AtendimentoEnvioService Envio;
        public required AtendimentoOrquestradorService Orquestrador;
        public required FakeAgente Agente;
        public required FakeCanal Canal;
        public required Classificador Gemini;
        public static async Task<Fixture> Create(CanalAtendimento canal, string texto)
        {
            var db = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var clock = new Clock();
            var paciente = new Paciente { Id = 1, Nome = "Paciente fictício", Cpf = "52998224725", Telefone = "5511987654321", Ativo = true };
            db.Pacientes.Add(paciente);
            var conversa = new ConversaAtendimento { Id = 1, Canal = canal, IdentificadorExterno = canal == CanalAtendimento.Email ? "ficticio@example.test" : paciente.Telefone, PacienteId = canal == CanalAtendimento.WhatsApp ? 1 : null, Modo = ModoAtendimento.Humano, SessionId = "antes" };
            db.ConversasAtendimento.Add(conversa);
            var entrada = new MensagemAtendimento { Id = 1, ConversaAtendimentoId = 1, Direcao = DirecaoMensagemAtendimento.Entrada, Autor = AutorMensagemAtendimento.Paciente, Texto = texto, Status = StatusMensagemAtendimento.Recebida };
            db.MensagensAtendimento.Add(entrada);await db.SaveChangesAsync();
            var conversas = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db));
            var fake = new FakeCanal { Canal = canal };
            var envio = new AtendimentoEnvioService([fake], db, TimeProvider.System, NullLogger<AtendimentoEnvioService>.Instance, Options.Create(new GmailInboxOptions()));
            var handler = new Classificador();
            var client = new GeminiClient(new HttpClient(handler), Options.Create(new GeminiOptions { ApiKey = "teste-apenas", TimeoutSeconds = 10 }), NullLogger<GeminiClient>.Instance);
            var decisao = new AtendimentoEmailDecisaoService(client, NullLogger<AtendimentoEmailDecisaoService>.Instance);
            var agente = new FakeAgente();
            var solicitacoes = new SolicitacaoAtendimentoService(db, new AuditoriaService(db, new HttpContextAccessor(), NullLogger<AuditoriaService>.Instance));
            var orq = new AtendimentoOrquestradorService(agente, conversas, envio, decisao, new ConvenioService(clock), solicitacoes, NullLogger<AtendimentoOrquestradorService>.Instance);
            return new() { Db=db, Conversa=conversa, Entrada=entrada, Conversas=conversas, Envio=envio, Orquestrador=orq, Agente=agente, Canal=fake, Gemini=handler };
        }
        public AtendimentoController Controller()
        {
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,"Admin"),new Claim(ClaimTypes.NameIdentifier,"teste-admin")], "teste")) };
            return new AtendimentoController(Db,Conversas,Envio,Orquestrador,new UsuarioVinculoService(Db,null!)) { ControllerContext=new ControllerContext { HttpContext=http }, TempData=new TempDataDictionary(http,new TempData()) };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class FakeAgente : IAgenteClinicaService
    {
        public int Calls;public string? Texto;public AgenteUsuarioContexto? Usuario;public IReadOnlyList<MensagemHistoricoAgente>? Historico;
        public async Task<AgenteResposta> EnviarAsync(string mensagem,string? sessionId,AgenteUsuarioContexto usuario,IReadOnlyList<MensagemHistoricoAgente>? historicoCliente=null,CancellationToken cancellationToken=default)
        { Calls++;Texto=mensagem;Usuario=usuario;Historico=historicoCliente;await Task.Delay(5,cancellationToken);return new() { Resposta="Posso ajudar com informações sobre cardiologia e horários.",SessionId=sessionId ?? "teste" }; }
    }
    private sealed class FakeCanal : ICanalAtendimentoSender
    {
        public CanalAtendimento Canal { get; init; }public bool Configurado=>true;public int Calls;public bool Falhar;
        public Task<CanalEnvioResultado> EnviarAsync(string destinatario,string texto,string? assunto=null,CancellationToken ct=default,string? replyTo=null)
        {Calls++;return Task.FromResult(Falhar ? CanalEnvioResultado.Falha("Falha fictícia de transporte") : CanalEnvioResultado.Ok("teste-envio"));}
    }
    private sealed class Classificador : HttpMessageHandler
    {
        public int Calls;public string? Texto;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;using var doc=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));Texto=doc.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
            return new(HttpStatusCode.OK) { Content=new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"acao\\\":\\\"RASCUNHO\\\"}\"}]}}]}",Encoding.UTF8,"application/json") };
        }
    }
    private sealed class Clock : IClinicaClock { public DateTime Agora=>new(2026,11,1,9,0,0);public DateTime Hoje=>Agora.Date;public DateTime ConverterUtc(DateTime utc)=>utc;public DateTime ConverterParaUtc(DateTime local)=>local; }
    private sealed class TempData : ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){} }
}
