using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

internal static class EncerramentoIaChecks
{
    private const string Fim="Por agora estou satisfeito, nos falamos depois";
    public static async Task RunAsync(Action<bool,string> check)
    {
        check(new[]{Fim,"Não preciso de mais nada, obrigado","Já resolvi tudo","Até a próxima"}.All(x=>EncerramentoAtendimento.PodeSugerir(x)),"Encerramento reconhece intenção de despedida além de comando literal");
        check(new[]{"obrigado","certo","sim","Quero agendar","Não preciso de mais nada, mas qual o endereço?","Até depois, ainda quero saber os horários","Não quero mais remarcar"}.All(x=>!EncerramentoAtendimento.PodeSugerir(x)) && !EncerramentoAtendimento.PodeSugerir(Fim,true),"Agradecimento, pergunta nova e confirmação pendente não autorizam fechamento");
        var handler=new ToolHandler(); var options=Options.Create(new GeminiOptions{ApiKey="simulado"});
        var realAgent=new AgenteClinicaService(new GeminiClient(new HttpClient(handler),options,NullLogger<GeminiClient>.Instance),null!,options,NullLogger<AgenteClinicaService>.Instance);
        var reply=await realAgent.EnviarAsync(Fim,Guid.NewGuid().ToString(),new(){PacienteId=1,Canal="Site"});
        check(reply.EncerrarAtendimento && reply.Resposta==EncerramentoAtendimento.Despedida && handler.Calls==1,"Ferramenta real da IA sinaliza encerramento sem rodada extra ao modelo");
        handler.Calls=0;
        reply=await realAgent.EnviarAsync("Obrigado",Guid.NewGuid().ToString(),new(){PacienteId=1,Canal="Site"});
        check(!reply.EncerrarAtendimento && handler.Calls==2,"Backend rejeita fechamento indevido do modelo em agradecimento isolado");
        foreach(var canal in new[]{CanalAtendimento.WhatsApp,CanalAtendimento.Sms})
        {
            await using var f=await Fixture.Create(canal);
            await f.Orq.ProcessarEntradaAsync(new(){Canal=canal,Identificador=f.Conversa.IdentificadorExterno,Texto=Fim,MensagemExternaId="fim:1"});
            await f.Feedback.ProcessarEnviosAsync(); await f.Feedback.ProcessarEnviosAsync();
            check(!f.Conversa.Ativa && await f.Db.AvaliacoesAtendimento.CountAsync()==1 && f.Sender.Textos.Count==1 && !f.Sender.Textos[0].Contains("gostaria"),canal+": despedida inferida cria avaliação e convite direto uma única vez");
        }
        await using(var f=await Fixture.Create(CanalAtendimento.WhatsApp))
        {
            f.Agent.Before=()=>f.Conversas.RegistrarEntradaAsync(f.Conversa,"Mais uma pergunta sobre horários","nova");
            await f.Orq.ProcessarEntradaAsync(new(){Canal=f.Conversa.Canal,Identificador=f.Conversa.IdentificadorExterno,Texto=Fim,MensagemExternaId="fim"});
            check(f.Conversa.Ativa && !await f.Db.AvaliacoesAtendimento.AnyAsync() && f.Sender.Textos.Count==0,"Nova pergunta durante a geração impede convite de despedida obsoleto");
        }
        await using(var f=await Fixture.Create(CanalAtendimento.WhatsApp))
        {
            f.Agent.Before=()=>f.Conversas.SolicitarAtendimentoHumanoAsync(f.Conversa);
            await f.Orq.ProcessarEntradaAsync(new(){Canal=f.Conversa.Canal,Identificador=f.Conversa.IdentificadorExterno,Texto=Fim,MensagemExternaId="fim"});
            check(f.Conversa.Ativa && !await f.Db.AvaliacoesAtendimento.AnyAsync() && f.Sender.Textos.Count==0,"Tomada humana durante a resposta impede encerramento automático");
        }
        await using(var f=await Fixture.Create(CanalAtendimento.Sms))
        {
            var paciente=new Paciente{UsuarioId="titular",Nome="Teste",Cpf="52998224725",Telefone="5511999990000",Email="t@example.test",CanalPreferido="WhatsApp"};
            f.Db.Pacientes.Add(paciente); var prefs=new ContaPerfil{UsuarioId="titular",AvisosWhatsApp=false,AvisosSms=false,AvisosEmail=false}; f.Db.ContaPerfis.Add(prefs); await f.Db.SaveChangesAsync();
            check(await ContaAvisos.EscolherAsync(f.Db,paciente,f.Envio,default) is null,"Sem canais autorizados, avisos automáticos não usam fallback bloqueado");
            prefs.AvisosSms=true; await f.Db.SaveChangesAsync();
            check((await ContaAvisos.EscolherAsync(f.Db,paciente,f.Envio,default))?.Canal==CanalAtendimento.Sms,"Lembretes escolhem canal permitido e configurado");
            f.Db.ContaPerfis.Remove(prefs); await f.Db.SaveChangesAsync();
            check((await ContaAvisos.EscolherAsync(f.Db,paciente,f.Envio,default))?.Canal==CanalAtendimento.Sms,"Contas antigas sem preferências preservam os avisos atuais");
        }
        await using(var f=await Fixture.Create(CanalAtendimento.Web))
        {
            var services=new ServiceCollection(); services.AddLogging(); services.AddSingleton(f.Db); services.AddIdentityCore<Usuario>().AddRoles<IdentityRole>().AddEntityFrameworkStores<MKSANContext>(); using var provider=services.BuildServiceProvider();
            f.Db.Pacientes.Add(new(){UsuarioId="titular",Nome="Alex",Cpf="52998224725",Ativo=true}); await f.Db.SaveChangesAsync();
            var controller=new AgenteController(f.Agent,new UsuarioVinculoService(f.Db,provider.GetRequiredService<UserManager<Usuario>>()),new AgenteHistoricoService(f.Db,NullLogger<AgenteHistoricoService>.Instance),new ConvenioService(new ClinicaClock()),f.Conversas,NullLogger<AgenteController>.Instance,f.Feedback)
            {ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"titular"),new Claim(ClaimTypes.Role,"Paciente")],"teste"))}}};
            var result=(JsonResult)await controller.Enviar(new(){Mensagem=Fim},default); var json=JsonSerializer.Serialize(result.Value);
            check(json.Contains("\"encerrada\":true") && json.Contains("\"avaliacao\":{") && !f.Conversa.Ativa && await f.Db.AvaliacoesAtendimento.CountAsync()==1,"Site recebe cartão para avaliar diretamente ao encerrar por intenção");
            check(await f.Db.MensagensAtendimento.CountAsync(m=>m.Direcao==DirecaoMensagemAtendimento.Entrada && m.Texto==Fim)==1,"Despedida natural do site é preservada uma única vez no histórico");
            await f.Conversas.ObterOuCriarAsync(CanalAtendimento.Web,"titular");
            f.Agent.Before=()=>f.Conversas.SolicitarAtendimentoHumanoAsync(f.Conversa);
            var humana=(JsonResult)await controller.Enviar(new(){Mensagem=Fim},default);
            check(JsonSerializer.Serialize(humana.Value).Contains("\"modo\":\"Humano\"") && f.Conversa.Ativa && await f.Db.AvaliacoesAtendimento.CountAsync()==1,
                "Site preserva tomada humana durante despedida e não cria outra avaliação");
        }
    }
    private sealed class ToolHandler:HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
        {
            Calls++; var part=Calls%2==1?"{\"functionCall\":{\"name\":\"encerrar_atendimento\",\"args\":{}}}":"{\"text\":\"Continuo à disposição.\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":["+part+"]}}]}",Encoding.UTF8,"application/json")});
        }
    }
    private sealed class FakeAgent:IAgenteClinicaService
    {
        public Func<Task>? Before;
        public async Task<AgenteResposta> EnviarAsync(string mensagem,string? sessionId,AgenteUsuarioContexto usuario,IReadOnlyList<MensagemHistoricoAgente>? historicoCliente=null,CancellationToken cancellationToken=default)
        {if(Before is not null)await Before();return new(){Resposta=EncerramentoAtendimento.Despedida,SessionId=sessionId??"",EncerrarAtendimento=true};}
    }
    private sealed class FakeSender:ICanalAtendimentoSender
    {
        public CanalAtendimento Canal{get;init;} public bool Configurado=>true; public List<string> Textos=[];
        public Task<CanalEnvioResultado> EnviarAsync(string destinatario,string texto,string? assunto=null,CancellationToken ct=default,string? replyTo=null){Textos.Add(texto);return Task.FromResult(CanalEnvioResultado.Ok("simulado:"+Textos.Count));}
    }
    private sealed class Fixture:IAsyncDisposable
    {
        public required MKSANContext Db;public required ConversaAtendimento Conversa;public required AtendimentoConversaService Conversas;public required AtendimentoFeedbackService Feedback;public required AtendimentoEnvioService Envio;public required AtendimentoOrquestradorService Orq;public required FakeAgent Agent;public required FakeSender Sender;
        public static async Task<Fixture> Create(CanalAtendimento canal)
        {
            var db=new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase("fim-"+Guid.NewGuid()).Options);
            var conversas=new AtendimentoConversaService(db,new AtendimentoIdentidadeService(db));
            var conversa=new ConversaAtendimento{Canal=canal,IdentificadorExterno=canal==CanalAtendimento.Web?"titular":"5511999990000",SessionId=Guid.NewGuid().ToString(),Modo=ModoAtendimento.IA}; db.Add(conversa);await db.SaveChangesAsync();
            var sender=new FakeSender{Canal=canal};var envio=new AtendimentoEnvioService([sender],db,TimeProvider.System,NullLogger<AtendimentoEnvioService>.Instance,Options.Create(new GmailInboxOptions()));
            var feedback=new AtendimentoFeedbackService(db,conversas,envio,TimeProvider.System);var agent=new FakeAgent();
            var orq=new AtendimentoOrquestradorService(agent,conversas,envio,null!,new ConvenioService(new ClinicaClock()),null!,NullLogger<AtendimentoOrquestradorService>.Instance,Options.Create(new AtendimentoEmailOptions()),feedback);
            return new(){Db=db,Conversa=conversa,Conversas=conversas,Feedback=feedback,Envio=envio,Orq=orq,Agent=agent,Sender=sender};
        }
        public ValueTask DisposeAsync()=>Db.DisposeAsync();
    }
}
