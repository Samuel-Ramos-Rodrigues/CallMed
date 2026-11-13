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
        await using (var queued = await Fixture.Create(CanalAtendimento.WhatsApp, "Olá"))
        {
            await queued.Conversas.DevolverParaIaAsync(queued.Conversa);
            var entry = new CanalMensagemEntrada {Canal=CanalAtendimento.WhatsApp,Identificador=queued.Conversa.IdentificadorExterno,Texto="Qual o horário de funcionamento?",MensagemExternaId="audit-pending-event"};
            await queued.Orquestrador.ProcessarEntradaAsync(entry,default,somenteRegistrar:true);
            check(queued.Agente.Calls==0,"A17: webhook persiste entrada sem aguardar a IA");
            using var cancelled=new CancellationTokenSource();queued.Agente.DuranteGeracao=()=>{cancelled.Cancel();return Task.CompletedTask;};
            try{await queued.Orquestrador.RetomarUltimaEntradaPendenteAsync(queued.Conversa,cancelled.Token);}catch(OperationCanceledException){}
            var count=await queued.Db.MensagensAtendimento.CountAsync();queued.Agente.DuranteGeracao=null;
            await queued.Orquestrador.ProcessarEntradaAsync(entry,default,somenteRegistrar:true);
            check(await queued.Db.MensagensAtendimento.CountAsync()==count,"A17: repetição do webhook não duplica a entrada persistida");
            await MKSANCrud.Services.Atendimento.Canais.WhatsApp.WhatsAppPendingBackgroundService.ProcessarAsync(queued.Db,queued.Orquestrador,default);
            check(queued.Agente.Calls==2&&queued.Canal.Calls==1,"A17: fila recupera geração cancelada e envia uma única resposta");
            await MKSANCrud.Services.Atendimento.Canais.WhatsApp.WhatsAppPendingBackgroundService.ProcessarAsync(queued.Db,queued.Orquestrador,default);
            check(queued.Canal.Calls==1,"A17: nova varredura não repete resposta concluída");
        }

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
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Oi, quais médicos têm aí?"))
        {
            var inicial = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(inicial.Estado == "humano" && inicial.PodeGerarRascunho && !inicial.Trabalhando,
                "Conversa antiga mostra IA pausada e permite gerar rascunho");
            f.Conversa.ResponsavelUsuarioId = "atendente-ficticio";
            await f.Db.SaveChangesAsync();
            await f.Controller().GerarRascunhoPendente(1, default);
            var pronto = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(pronto.Estado == "rascunho" && !pronto.PodeGerarRascunho && !pronto.Trabalhando && f.Canal.Calls == 0,
                "Gerar rascunho em conversa antiga conclui com aprovação pendente e não envia e-mail");
            check(f.Conversa.Modo == ModoAtendimento.Humano && f.Conversa.ResponsavelUsuarioId == "atendente-ficticio",
                "Gerar rascunho preserva modo humano e responsável atual");
            await f.Controller().GerarRascunhoPendente(1, default);
            check(f.Agente.Calls == 1, "Novo clique não duplica um rascunho aguardando aprovação");
            var draft = await f.Db.MensagensAtendimento.SingleAsync(m => m.Status == StatusMensagemAtendimento.AguardandoAprovacao);
            await f.Envio.DescartarRascunhoAsync(draft.Id, "atendente-ficticio");
            var descartado = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(descartado.Estado == "pendente" && descartado.PodeGerarRascunho,
                "Rascunho descartado deixa a mensagem disponível para nova tentativa");
            await f.Controller().GerarRascunhoPendente(1, default);
            check(f.Agente.Calls == 2 && await f.Db.MensagensAtendimento.CountAsync(m => m.Status == StatusMensagemAtendimento.AguardandoAprovacao) == 1 &&
                await f.Db.MensagensAtendimento.CountAsync(m => m.Status == StatusMensagemAtendimento.Ignorada) == 1,
                "Regeneração cria uma nova sugestão e preserva o rascunho descartado no histórico");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?"))
        {
            var analisando = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberarAnalise = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var gerando = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var liberarGeracao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            f.Gemini.DuranteAnalise = async () => { analisando.SetResult(); await liberarAnalise.Task; };
            f.Agente.DuranteGeracao = async () => { gerando.SetResult(); await liberarGeracao.Task; };
            f.Conversa.Modo = ModoAtendimento.IA;
            await f.Db.SaveChangesAsync();
            var processo = f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(new CanalMensagemEntrada {
                Canal = CanalAtendimento.Email, Identificador = f.Conversa.IdentificadorExterno,
                Texto = "Quais médicos têm aí?", MensagemExternaId = "gmail:progresso" });
            await analisando.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var estado = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(estado.Estado == "analisando" && estado.Trabalhando && !estado.PodeGerarRascunho,
                "Entrada automática persiste o estado analisando antes da classificação");
            liberarAnalise.SetResult();
            await gerando.Task.WaitAsync(TimeSpan.FromSeconds(10));
            estado = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(estado.Estado == "gerando" && estado.Trabalhando,
                "Durante a chamada ao agente o estado real é gerando rascunho");
            liberarGeracao.SetResult(); await processo;
            check((await f.Conversas.ObterEstadoIaAsync(f.Conversa, default)).Estado == "rascunho",
                "Conclusão automática substitui o andamento por rascunho pronto");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?"))
        {
            f.Agente.Falhar = true;
            await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa);
            var estado = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(estado.Estado == "falha" && !estado.Trabalhando && estado.PodeGerarRascunho && f.Canal.Calls == 0,
                "Falha do agente fica visível e permite nova tentativa sem enviar e-mail");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Meu CPF é 52998224725"))
        {
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check((await f.Conversas.ObterEstadoIaAsync(f.Conversa, default)).Estado == "equipe",
                "Pedido que exige equipe é distinguido de falha técnica da IA");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?"))
        {
            f.Gemini.Indisponivel = true;
            await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa);
            check((await f.Conversas.ObterEstadoIaAsync(f.Conversa, default)).Estado == "falha" && f.Agente.Calls == 0,
                "Classificador indisponível aparece como falha, sem simular análise concluída");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?"))
        {
            f.Conversa.IaEntradaId = f.Entrada.Id; f.Conversa.IaEstado = "gerando";
            f.Conversa.IaAtualizadoEm = DateTime.UtcNow.AddMinutes(-11); await f.Db.SaveChangesAsync();
            var estado = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(estado.Estado == "interrompida" && !estado.Trabalhando && estado.PodeGerarRascunho,
                "Processamento abandonado após reinício não deixa animação infinita");
            f.Conversa.Ativa = false; await f.Db.SaveChangesAsync();
            check(!(await f.Conversas.ObterEstadoIaAsync(f.Conversa, default)).PodeGerarRascunho,
                "Conversa encerrada não permite gerar rascunho");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?"))
        {
            using var cts = new CancellationTokenSource();
            f.Agente.DuranteGeracao = () => { cts.Cancel(); return Task.CompletedTask; };
            try { await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa, cts.Token); }
            catch (OperationCanceledException) { }
            check((await f.Conversas.ObterEstadoIaAsync(f.Conversa, default)).Estado == "falha",
                "Cancelamento da chamada encerra o estado de trabalho persistido");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Pergunta antiga"))
        {
            f.Agente.DuranteGeracao = async () => {
                await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Nova pergunta", "teste:durante-geracao");
            };
            await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa);
            var estado = await f.Conversas.ObterEstadoIaAsync(f.Conversa, default);
            check(estado.Estado == "humano" && estado.PodeGerarRascunho &&
                !await f.Db.MensagensAtendimento.AnyAsync(m => m.Status == StatusMensagemAtendimento.AguardandoAprovacao),
                "Entrada nova durante a geração não é marcada como respondida pelo rascunho da pergunta antiga");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?"))
        {
            var controller = f.Controller();
            controller.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            check(await controller.GerarRascunhoPendente(1, default) is JsonResult,
                "Ação assíncrona devolve estado da IA em JSON com autorização e antiforgery preservados");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Preciso de cardiologista"))
        {
            const string pergunta = "Deseja que eu verifique a disponibilidade de horários do cardiologista?";
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, pergunta, AutorMensagemAtendimento.Assistente, StatusMensagemAtendimento.Enviada);
            var atual = await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Sim pfvr", "gmail:aceite-vagas");
            var resultado = await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Gemini.Texto == "Sim pfvr" && f.Gemini.Historico?.Last().Texto == pergunta,
                "Triagem recebe o Sim pfvr junto da pergunta real anterior sobre disponibilidade");
            check(f.Gemini.Historico?.Count == 2 && f.Gemini.Historico.All(m => m.Texto != "Sim pfvr"),
                "Mensagem atual é separada do contexto e aparece uma única vez no pedido de classificação");
            check(resultado == ResultadoRetomadaAtendimento.RascunhoCriado && f.Canal.Calls == 0 &&
                f.Agente.Historico?.Last().Texto == pergunta && f.Agente.Texto == "Sim pfvr",
                "Decisão pública continua no agente com o mesmo contexto e prepara rascunho sem enviar");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Meu CPF é 52998224725"))
        {
            const string pergunta = "Deseja consultar horários? Meu telefone é 11987654321";
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, pergunta, AutorMensagemAtendimento.Funcionario, StatusMensagemAtendimento.Enviada);
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, "Rascunho que não foi enviado", AutorMensagemAtendimento.Assistente, StatusMensagemAtendimento.AguardandoAprovacao);
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, "Resposta que falhou", AutorMensagemAtendimento.Assistente, StatusMensagemAtendimento.Falhou);
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, "Aviso interno do sistema", AutorMensagemAtendimento.Sistema, StatusMensagemAtendimento.Enviada);
            f.Db.ConversasAtendimento.Add(new ConversaAtendimento { Id = 2, Canal = CanalAtendimento.Email, IdentificadorExterno = "outro@example.test", SessionId = "outra" });
            f.Db.MensagensAtendimento.Add(new MensagemAtendimento { ConversaAtendimentoId = 2, Texto = "Conteúdo de outra conversa", Direcao = DirecaoMensagemAtendimento.Entrada, Autor = AutorMensagemAtendimento.Paciente });
            await f.Db.SaveChangesAsync();
            await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Sim pfvr", "gmail:historico-seguro");
            await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa);
            check(f.Gemini.Historico?.Count == 1 && f.Gemini.Historico[0].Papel == "bot",
                "Contexto da triagem exclui CPF antigo, rascunho não enviado, falha, sistema e outra conversa");
            check(f.Gemini.Pedido?.Contains("52998224725") == false && f.Gemini.Pedido.Contains("11987654321") == false &&
                f.Gemini.Historico?[0].Texto.Contains("[telefone omitido]") == true,
                "Histórico é sanitizado antes de ser enviado ao classificador");
            check(f.Agente.Historico?.Count == 1 && f.Agente.Historico[0].Texto == f.Gemini.Historico?[0].Texto,
                "Classificador e agente compartilham o histórico seguro da mesma conversa");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quero cancelar minha consulta"))
        {
            const string pergunta = "Você confirma que deseja cancelar sua consulta pessoal?";
            await f.Conversas.RegistrarSaidaAsync(f.Conversa, pergunta, AutorMensagemAtendimento.Funcionario, StatusMensagemAtendimento.Enviada);
            await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Sim pfvr", "gmail:aceite-privado");
            f.Gemini.Acao = "EQUIPE"; // Decisão externa simulada, não um teste semântico do Gemini real.
            var resultado = await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Gemini.Historico?.Last().Texto == pergunta && resultado == ResultadoRetomadaAtendimento.RequerEquipe && f.Agente.Calls == 0,
                "Confirmação privada mantém contexto e respeita encaminhamento sem executar operação");
        }
        await RunAutomaticoAsync(check);
    }

    private static AtendimentoEmailAutomaticoService Worker(Fixture f) => new(f.Db, f.Conversas, f.Orquestrador,
        Options.Create(new AtendimentoEmailOptions()), NullLogger<AtendimentoEmailAutomaticoService>.Instance);

    private static async Task RunAutomaticoAsync(Action<bool, string> check)
    {
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quero marcar uma consulta com o dr david", true))
        {
            f.Conversa.IaEstado = "equipe"; f.Conversa.IaEntradaId = f.Entrada.Id;
            await f.Db.SaveChangesAsync();
            check(await Worker(f).ProcessarPendenciasAsync() == 1 && f.Canal.Calls == 1 && f.Conversa.Modo == ModoAtendimento.IA,
                "Pendência recente da política antiga é reanalisada e respondida sem clique nem aprovação");
            var enviada = await f.Db.MensagensAtendimento.SingleAsync(m => m.Direcao == DirecaoMensagemAtendimento.Saida);
            check(enviada.Status == StatusMensagemAtendimento.Enviada && enviada.Texto.Contains("https://portal.example.test/Consulta/Create") &&
                f.Agente.Usuario is { PacienteId: null, EmailRespostaAutomatica: true } && !await f.Db.Consultas.AnyAsync(),
                "Resposta automática oferece portal, não autentica remetente e não cria consulta");
            check(await Worker(f).ProcessarPendenciasAsync() == 0 && f.Canal.Calls == 1,
                "Novo ciclo não repete o e-mail automático já enviado");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Primeiro pedido", true))
        {
            await f.Conversas.DevolverParaIaAsync(f.Conversa);
            var entrada = new CanalMensagemEntrada { Canal = CanalAtendimento.Email, Identificador = f.Conversa.IdentificadorExterno,
                Texto = "Quais médicos atendem?", MensagemExternaId = "gmail:auto-novo" };
            await f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(entrada);
            await f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(entrada);
            check(f.Canal.Calls == 1 && await f.Db.MensagensAtendimento.CountAsync(m => m.Status == StatusMensagemAtendimento.AguardandoAprovacao) == 0,
                "Entrada Gmail nova é respondida uma vez sem depender de Aprovar e enviar");
            entrada = new CanalMensagemEntrada { Canal = CanalAtendimento.Email, Identificador = f.Conversa.IdentificadorExterno,
                Texto = "E amanhã?", MensagemExternaId = "gmail:auto-seguinte" };
            await f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(entrada);
            check(f.Canal.Calls == 2 && f.Agente.Historico!.Any(m => m.Papel == "bot"),
                "Próxima resposta do paciente mantém continuidade automática com o histórico enviado");
            await f.Conversas.AssumirAsync(f.Conversa, "atendente-teste");
            entrada = new CanalMensagemEntrada { Canal = CanalAtendimento.Email, Identificador = f.Conversa.IdentificadorExterno,
                Texto = "Mais uma pergunta", MensagemExternaId = "gmail:auto-pausado" };
            await f.Orquestrador.ProcessarEntradaEmailComoRascunhoAsync(entrada);
            check(f.Canal.Calls == 2 && await Worker(f).ProcessarPendenciasAsync() == 0,
                "Assumir atendimento pausa o envio automático e a retomada respeita o responsável");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quero cancelar minha consulta", true))
        {
            f.Gemini.Acao = "PORTAL";
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            var saida = await f.Db.MensagensAtendimento.SingleAsync(m => m.Direcao == DirecaoMensagemAtendimento.Saida);
            check(f.Agente.Calls == 0 && f.Canal.Calls == 1 && saida.Texto.Contains("https://portal.example.test/Consulta") && !await f.Db.Consultas.AnyAsync(),
                "Operação privada recebe orientação automática para o portal, sem chamada de ferramenta privada");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Primeiro pedido", true))
        {
            await f.Conversas.DevolverParaIaAsync(f.Conversa);
            await f.Orquestrador.ProcessarEntradaAsync(new CanalMensagemEntrada { Canal = CanalAtendimento.Email,
                Identificador = f.Conversa.IdentificadorExterno, ConversaAtendimentoId = f.Conversa.Id,
                MensagemExternaId = "webhook:auto", Texto = "Quais horários do Dr. David?" });
            check(f.Canal.Calls == 1 && f.Gemini.Calls == 1 && f.Agente.Usuario?.PacienteId is null,
                "Entrada de e-mail por webhook segue a mesma triagem, contexto público e envio automático do Gmail");
            await f.Orquestrador.ProcessarEntradaAsync(new CanalMensagemEntrada { Canal = CanalAtendimento.Email,
                Identificador = "outro@example.test", ConversaAtendimentoId = f.Conversa.Id,
                MensagemExternaId = "webhook:outro", Texto = "Quais horários?" });
            check(f.Canal.Calls == 1 && f.Gemini.Calls == 1,
                "Alias de conversa continua rejeitando remetente diferente do contato original");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Meu CPF é 52998224725", true))
        {
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Gemini.Calls == 0 && f.Agente.Calls == 0 && f.Canal.Calls == 1 &&
                !(await f.Db.MensagensAtendimento.SingleAsync(m => m.Direcao == DirecaoMensagemAtendimento.Saida)).Texto.Contains("52998224725"),
                "Identificador sensível recebe orientação fixa sem exposição ao Gemini ou eco na resposta");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quero falar com um atendente", true))
        {
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Gemini.Calls == 0 && f.Agente.Calls == 0 && f.Canal.Calls == 1 && f.Conversa.Modo == ModoAtendimento.Humano,
                "Pedido explícito de atendente gera confirmação de recebimento e preserva fila humana");
            check(await Worker(f).ProcessarPendenciasAsync() == 0 && f.Canal.Calls == 1,
                "Retomada não desfaz o encaminhamento humano da política atual");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais horários?", true))
        {
            f.Agente.Falhar = true;
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Conversa.Modo == ModoAtendimento.IA && f.Conversa.IaTentativas == 1 && f.Canal.Calls == 0,
                "Falha técnica de análise preserva a automação sem enviar resposta fictícia");
            check(await Worker(f).ProcessarPendenciasAsync() == 0, "Falha técnica respeita espera antes da nova tentativa");
            for (var i = 0; i < 2; i++)
            {
                f.Conversa.IaAtualizadoEm = DateTime.UtcNow.AddMinutes(-2); await f.Db.SaveChangesAsync();
                await Worker(f).ProcessarPendenciasAsync();
            }
            f.Conversa.IaAtualizadoEm = DateTime.UtcNow.AddMinutes(-2); await f.Db.SaveChangesAsync();
            check(f.Conversa.IaTentativas == 3 && await Worker(f).ProcessarPendenciasAsync() == 0 && f.Canal.Calls == 0,
                "Análise com falha persistente para após três tentativas por mensagem");
            f.Agente.Falhar = false;
            await f.Conversas.RegistrarEntradaAsync(f.Conversa, "Quais médicos atendem?", "gmail:entrada-apos-interrupcao");
            check(await Worker(f).ProcessarPendenciasAsync() == 1 && f.Canal.Calls == 1 && f.Conversa.IaTentativas == 1,
                "Entrada nova persistida antes de uma interrupção retoma sozinha e renova o limite de tentativas");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?", true))
        {
            await f.Conversas.DevolverParaIaAsync(f.Conversa);
            f.Agente.Falhar = true;
            await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa);
            check(await Worker(f).ProcessarPendenciasAsync() == 0 && f.Canal.Calls == 0,
                "Falha ao gerar rascunho manual não vira envio automático em segundo plano");
            f.Agente.Falhar = false;
            await f.Orquestrador.GerarRascunhoEmailAsync(f.Conversa);
            check(f.Canal.Calls == 0 && (await f.Conversas.ObterEstadoIaAsync(f.Conversa, default)).Estado == "rascunho",
                "Gerar rascunho manual continua exigindo revisão mesmo com automação habilitada");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?", true))
        {
            f.Canal.Incerto = true;
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            var falha = await f.Db.MensagensAtendimento.SingleAsync(m => m.Direcao == DirecaoMensagemAtendimento.Saida);
            await Worker(f).ProcessarPendenciasAsync(); await f.Envio.ReenviarAsync(falha.Id);
            check(falha.Status == StatusMensagemAtendimento.Falhou && falha.ReenvioBloqueado && f.Canal.Calls == 1,
                "Envio sem confirmação bloqueia repetição automática e manual para evitar duplicidade");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quais médicos?", true))
        {
            f.Agente.DuranteGeracao = () => f.Conversas.AssumirAsync(f.Conversa, "atendente-durante-ia");
            await f.Orquestrador.DevolverParaIaERetomarAsync(f.Conversa);
            check(f.Canal.Calls == 0 && f.Conversa.ResponsavelUsuarioId == "atendente-durante-ia",
                "Tomada humana durante a geração impede o envio automático em andamento");
        }
        check(AgenteToolDefinitions.CriarParaEmail().Count == 6 &&
            AgenteToolDefinitions.CriarParaEmail().All(x => AgenteToolDefinitions.PermitidaNoEmail(x!["name"]!.GetValue<string>())),
            "Modelo do e-mail recebe somente as seis ferramentas autorizadas, incluindo encerramento");
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Pergunta antiga", true))
        {
            f.Conversa.IaEstado = "equipe"; f.Entrada.CriadoEm = DateTime.UtcNow.AddDays(-2);
            await f.Db.SaveChangesAsync();
            check(await Worker(f).ProcessarPendenciasAsync() == 0 && f.Canal.Calls == 0,
                "Retomada padrão não dispara respostas para mensagens com mais de 24 horas");
        }
        await using (var f = await Fixture.Create(CanalAtendimento.Email, "Quero marcar com o Dr. David", true))
        {
            f.Conversa.IaEstado = "equipe"; f.Conversa.IaAtualizadoEm = DateTime.UtcNow;
            for (var i = 2; i <= 26; i++)
            {
                f.Db.ConversasAtendimento.Add(new ConversaAtendimento { Id = i, Canal = CanalAtendimento.Email,
                    IdentificadorExterno = $"antigo{i}@example.test", SessionId = $"antigo{i}", Modo = ModoAtendimento.IA,
                    IaEstado = "falha", IaAtualizadoEm = DateTime.UtcNow.AddDays(-2) });
                f.Db.MensagensAtendimento.Add(new MensagemAtendimento { ConversaAtendimentoId = i,
                    Direcao = DirecaoMensagemAtendimento.Entrada, Autor = AutorMensagemAtendimento.Paciente,
                    Status = StatusMensagemAtendimento.Recebida, Texto = "Mensagem antiga", CriadoEm = DateTime.UtcNow.AddDays(-2) });
            }
            await f.Db.SaveChangesAsync();
            check(await Worker(f).ProcessarPendenciasAsync() == 1 && f.Canal.Calls == 1,
                "Mensagens antigas não ocupam o limite do lote nem bloqueiam uma pendência recente");
        }
        var captura = new CapturaEmail();
        var smtp = new MKSANCrud.Services.Atendimento.Canais.Email.SmtpAtendimentoSender(captura,
            Options.Create(new AtendimentoEmailOptions { PortalBaseUrl = "https://portal.example.test" }));
        await smtp.EnviarAsync("ficticio@example.test", "Escolha: https://portal.example.test/Consulta/Create?medicoId=1&data=2026-11-02&horario=10%3A00");
        check(captura.Html.Contains("href=\"https://portal.example.test/Consulta/Create?medicoId=1&amp;data=2026-11-02&amp;horario=10%3A00\""),
            "Link de conclusão no e-mail HTML preserva médico, data e horário e escapa os parâmetros");
        check(new AtendimentoEmailOptions { PortalBaseUrl = "javascript:alert(1)" }.UrlPortal("Consulta") is null &&
            new AtendimentoEmailOptions { PortalBaseUrl = "https://user:pass@example.test" }.UrlPortal("Consulta") is null,
            "Portal rejeita protocolo inseguro e credenciais embutidas na URL");
        await CheckAgendaRealAsync(check);
    }

    private sealed class CapturaEmail : MKSANCrud.Services.Email.IEmailService
    {
        public bool Configurado => true;
        public string Html = string.Empty;
        public Task<bool> EnviarAsync(string destinatario, string assunto, string corpoHtml, CancellationToken ct = default, string? replyTo = null)
        { Html = corpoHtml; return Task.FromResult(true); }
    }

    private static async Task CheckAgendaRealAsync(Action<bool, string> check)
    {
        await using var f = await Fixture.Create(CanalAtendimento.Email, "Quero marcar uma consulta com o dr david", true);
        var clock = new Clock();
        f.Db.AddRange(new Especialidade { Id = 1, Nome = "Urologia", Ativo = true },
            new Medico { Id = 1, Nome = "Dr. David Kaio", Especialidade = "Urologia", EspecialidadeId = 1, Ativo = true },
            new Disponibilidade { Id = 1, MedicoId = 1, Data = clock.Hoje.AddDays(1), Horario = "10:00", Ativo = true });
        await f.Db.SaveChangesAsync();
        var especialidades = new EspecialidadeService(f.Db); var convenio = new ConvenioService(clock);
        var agenda = new MKSANCrud.Services.Agendamento.AgendamentoService(f.Db, clock, especialidades, convenio,
            new ConvenioElegibilidadeService(f.Db, convenio), NullLogger<MKSANCrud.Services.Agendamento.AgendamentoService>.Instance);
        var solicitacoes = new SolicitacaoAtendimentoService(f.Db, new AuditoriaService(f.Db, new HttpContextAccessor(), NullLogger<AuditoriaService>.Instance));
        var tools = new AgenteToolsService(f.Db, null!, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), agenda,
            especialidades, convenio, clock, null!, solicitacoes, f.Conversas, NullLogger<AgenteToolsService>.Instance);
        var script = new RoteiroAgenda();
        var geminiOptions = Options.Create(new GeminiOptions { ApiKey = "teste-apenas", TimeoutSeconds = 10 });
        var gemini = new GeminiClient(new HttpClient(script), geminiOptions, NullLogger<GeminiClient>.Instance);
        var real = new AgenteClinicaService(gemini, tools, geminiOptions, NullLogger<AgenteClinicaService>.Instance);
        var triagem = new AtendimentoEmailDecisaoService(new GeminiClient(new HttpClient(f.Gemini), geminiOptions, NullLogger<GeminiClient>.Instance), NullLogger<AtendimentoEmailDecisaoService>.Instance);
        var orq = new AtendimentoOrquestradorService(real, f.Conversas, f.Envio, triagem, convenio, solicitacoes,
            NullLogger<AtendimentoOrquestradorService>.Instance, Options.Create(new AtendimentoEmailOptions { PortalBaseUrl = "https://portal.example.test" }));
        await orq.DevolverParaIaERetomarAsync(f.Conversa);
        var enviada = await f.Db.MensagensAtendimento.SingleAsync(m => m.Direcao == DirecaoMensagemAtendimento.Saida);
        check(script.Publicas && script.Calls == 2 && f.Canal.Calls == 1 && enviada.Status == StatusMensagemAtendimento.Enviada &&
            enviada.Texto.Contains("Dr. David Kaio") && enviada.Texto.Contains("10:00") && enviada.Texto.Contains("medicoId=1&data=2026-11-02&horario=10%3A00"),
            "Fluxo com agente e agenda reais consulta vaga do Dr. David e envia link preenchido, usando Gemini simulado");
        check(!await f.Db.Consultas.AnyAsync() && (await f.Db.Disponibilidades.SingleAsync()).Ativo,
            "Oferta de vaga por e-mail mantém o horário livre até o paciente concluir no portal");
    }

    private sealed class RoteiroAgenda : HttpMessageHandler
    {
        public int Calls; public bool Publicas = true;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var root = System.Text.Json.Nodes.JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
            Publicas &= root["tools"]![0]!["functionDeclarations"]!.AsArray().Count == 6;
            System.Text.Json.Nodes.JsonObject part;
            if (Calls == 1)
                part = new() { ["functionCall"] = new System.Text.Json.Nodes.JsonObject { ["name"] = "buscar_opcoes_agendamento",
                    ["args"] = new System.Text.Json.Nodes.JsonObject { ["nomeMedico"] = "David", ["especialidade"] = "" } } };
            else
            {
                var opcao = root["contents"]!.AsArray().Last()!["parts"]![0]!["functionResponse"]!["response"]!["result"]!["dados"]!["opcoes"]![0]!;
                part = new() { ["text"] = $"Encontrei {opcao["medico"]!.GetValue<string>()} às {opcao["horario"]!.GetValue<string>()}. Conclua pela sua conta: {opcao["urlAgendamento"]!.GetValue<string>()}" };
            }
            var response = new System.Text.Json.Nodes.JsonObject { ["candidates"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject {
                ["content"] = new System.Text.Json.Nodes.JsonObject { ["role"] = "model", ["parts"] = new System.Text.Json.Nodes.JsonArray(part) } }) };
            return new(HttpStatusCode.OK) { Content = new StringContent(response.ToJsonString(), Encoding.UTF8, "application/json") };
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
        public static async Task<Fixture> Create(CanalAtendimento canal, string texto, bool automatico = false)
        {
            var db = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var clock = new Clock();
            var paciente = new Paciente { Id = 1, Nome = "Paciente fictício", Cpf = "52998224725", Telefone = "5511987654321", Ativo = true };
            db.Pacientes.Add(paciente);
            var conversa = new ConversaAtendimento { Id = 1, Canal = canal, IdentificadorExterno = canal == CanalAtendimento.Email ? "ficticio@example.test" : paciente.Telefone, PacienteId = canal == CanalAtendimento.WhatsApp ? 1 : null, Modo = ModoAtendimento.Humano, SessionId = "antes" };
            db.ConversasAtendimento.Add(conversa);
            var entrada = new MensagemAtendimento { Id = 1, ConversaAtendimentoId = 1, Direcao = DirecaoMensagemAtendimento.Entrada, Autor = AutorMensagemAtendimento.Paciente, Texto = texto, Status = StatusMensagemAtendimento.Recebida };
            db.MensagensAtendimento.Add(entrada);await db.SaveChangesAsync();
            var emailOptions = Options.Create(new AtendimentoEmailOptions { EnviarRespostasAutomaticamente = automatico, PortalBaseUrl = "https://portal.example.test" });
            var conversas = new AtendimentoConversaService(db, new AtendimentoIdentidadeService(db), emailOptions);
            var fake = new FakeCanal { Canal = canal };
            var envio = new AtendimentoEnvioService([fake], db, TimeProvider.System, NullLogger<AtendimentoEnvioService>.Instance, Options.Create(new GmailInboxOptions()));
            var handler = new Classificador();
            var client = new GeminiClient(new HttpClient(handler), Options.Create(new GeminiOptions { ApiKey = "teste-apenas", TimeoutSeconds = 10 }), NullLogger<GeminiClient>.Instance);
            var decisao = new AtendimentoEmailDecisaoService(client, NullLogger<AtendimentoEmailDecisaoService>.Instance);
            var agente = new FakeAgente();
            var solicitacoes = new SolicitacaoAtendimentoService(db, new AuditoriaService(db, new HttpContextAccessor(), NullLogger<AuditoriaService>.Instance));
            var orq = new AtendimentoOrquestradorService(agente, conversas, envio, decisao, new ConvenioService(clock), solicitacoes, NullLogger<AtendimentoOrquestradorService>.Instance, emailOptions);
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
        public Func<Task>? DuranteGeracao; public bool Falhar;
        public int Calls;public string? Texto;public AgenteUsuarioContexto? Usuario;public IReadOnlyList<MensagemHistoricoAgente>? Historico;
        public async Task<AgenteResposta> EnviarAsync(string mensagem,string? sessionId,AgenteUsuarioContexto usuario,IReadOnlyList<MensagemHistoricoAgente>? historicoCliente=null,CancellationToken cancellationToken=default)
        { Calls++;Texto=mensagem;Usuario=usuario;Historico=historicoCliente;
            if (DuranteGeracao is not null) await DuranteGeracao();
            if (Falhar) throw new InvalidOperationException("Falha fictícia da IA");
            await Task.Delay(5,cancellationToken);return new() { Resposta="Posso ajudar com informações sobre cardiologia e horários.",SessionId=sessionId ?? "teste" }; }
    }
    private sealed class FakeCanal : ICanalAtendimentoSender
    {
        public CanalAtendimento Canal { get; init; }public bool Configurado=>true;public int Calls;public bool Falhar;public bool Incerto;
        public Task<CanalEnvioResultado> EnviarAsync(string destinatario,string texto,string? assunto=null,CancellationToken ct=default,string? replyTo=null)
        {Calls++;if(Incerto)throw new HttpRequestException("Envio sem confirmação no teste");return Task.FromResult(Falhar ? CanalEnvioResultado.Falha("Falha fictícia de transporte") : CanalEnvioResultado.Ok("teste-envio"));}
    }
    private sealed class Classificador : HttpMessageHandler
    {
        public Func<Task>? DuranteAnalise; public bool Indisponivel;
        public int Calls; public string? Texto; public string? Pedido; public string Acao = "RASCUNHO";
        public List<MensagemHistoricoAgente>? Historico;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if (DuranteAnalise is not null) await DuranteAnalise();
            if (Indisponivel) return new(HttpStatusCode.ServiceUnavailable);
            Calls++;
            using var doc = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Pedido = doc.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
            using var dados = System.Text.Json.JsonDocument.Parse(Pedido!);
            Texto = dados.RootElement.GetProperty("mensagemAtual").GetString();
            Historico = dados.RootElement.GetProperty("historico").EnumerateArray()
                .Select(m => new MensagemHistoricoAgente { Papel = m.GetProperty("papel").GetString()!, Texto = m.GetProperty("texto").GetString()! }).ToList();
            var resposta = System.Text.Json.JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = System.Text.Json.JsonSerializer.Serialize(new { acao = Acao }) } } } } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(resposta, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Clock : IClinicaClock { public DateTime Agora=>new(2026,11,1,9,0,0);public DateTime Hoje=>Agora.Date;public DateTime ConverterUtc(DateTime utc)=>utc;public DateTime ConverterParaUtc(DateTime local)=>local; }
    private sealed class TempData : ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){} }
}
