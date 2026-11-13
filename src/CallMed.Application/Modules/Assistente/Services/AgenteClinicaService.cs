using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.DTOs.Agente;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MKSANCrud.Services.Agente;

public sealed partial class AgenteClinicaService : IAgenteClinicaService
{
    private sealed class Sessao
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public List<JsonObject> Historico { get; } = [];
        public DateTime UltimoUsoUtc { get; set; } = DateTime.UtcNow;

        public JsonObject? UltimaBuscaAgendamentoArgs { get; set; }
        public JsonObject? UltimoResultadoAgendamento { get; set; }

        // A confirmação de uma mutação vale apenas para o payload preparado.
        public string? MutacaoPendenteNome { get; set; }
        public string? MutacaoPendenteChave { get; set; }
        public JsonObject? MutacaoPendenteArgs { get; set; }
        public DateTime? MutacaoPendenteCriadaUtc { get; set; }
    }

    private static readonly ConcurrentDictionary<string, Sessao> Sessoes =
        new(StringComparer.Ordinal);

    private readonly GeminiClient _gemini;
    private readonly AgenteToolsService _tools;
    private readonly GeminiOptions _options;
    private readonly ILogger<AgenteClinicaService> _logger;

    public AgenteClinicaService(
        GeminiClient gemini,
        AgenteToolsService tools,
        IOptions<GeminiOptions> options,
        ILogger<AgenteClinicaService> logger)
    {
        _gemini = gemini;
        _tools = tools;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgenteResposta> EnviarAsync(
        string mensagem,
        string? sessionId,
        AgenteUsuarioContexto usuario,
        IReadOnlyList<MensagemHistoricoAgente>? historicoCliente = null,
        CancellationToken cancellationToken = default)
    {
        if (!_gemini.Configurado)
            throw new InvalidOperationException("Gemini:ApiKey não configurada.");

        var textoUsuario = (mensagem ?? string.Empty).Trim();
        var sid = NormalizarSessionId(sessionId);

        // O mesmo sessionId nunca compartilha memória entre contas diferentes.
        var escopoUsuario = string.IsNullOrWhiteSpace(usuario.Email)
            ? usuario.PacienteId?.ToString(CultureInfo.InvariantCulture) ?? "usuario"
            : usuario.Email.Trim().ToLowerInvariant();
        var chaveSessao = $"{escopoUsuario}::{sid}";
        var sessao = Sessoes.GetOrAdd(chaveSessao, _ => new Sessao());

        await sessao.Lock.WaitAsync(cancellationToken);

        try
        {
            sessao.UltimoUsoUtc = DateTime.UtcNow;

            if (usuario.Canal.Equals("E-mail", StringComparison.OrdinalIgnoreCase))
            {
                // Somente falas efetivamente recebidas/enviadas entram no próximo
                // e-mail. Sugestões manuais não enviadas não viram contexto do paciente.
                sessao.Historico.Clear();
                sessao.UltimaBuscaAgendamentoArgs = null;
                sessao.UltimoResultadoAgendamento = null;
                LimparMutacaoPendente(sessao);
            }
            if (sessao.Historico.Count == 0)
                RecuperarHistoricoCliente(sessao.Historico, historicoCliente);

            if (MutacaoPendenteExpirou(sessao) || EhRecusaExplicita(textoUsuario))
                LimparMutacaoPendente(sessao);

            if (NormalizarTexto(textoUsuario) is "oi" or "ola" or "oie" or "oii" or "bom dia" or "boa tarde" or "boa noite")
            {
                var nome = usuario.PacienteNome?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var saudacao = string.IsNullOrWhiteSpace(nome) ? "Olá!" : $"Olá, {nome}!";
                var resposta = $"{saudacao} Como posso ajudar você na CallMed?";
                sessao.Historico.Add(ConteudoTexto("user", textoUsuario));
                sessao.Historico.Add(ConteudoTexto("model", resposta));
                ApararHistorico(sessao.Historico);
                LimparSessoesAntigas();
                return new AgenteResposta { Resposta = resposta, SessionId = sid };
            }

            if (EhPedidoReconsulta(textoUsuario) &&
                sessao.UltimaBuscaAgendamentoArgs is not null)
            {
                // Reconsultar disponibilidade invalida qualquer confirmação antiga.
                LimparMutacaoPendente(sessao);
                sessao.Historico.Add(ConteudoTexto("user", textoUsuario));

                var resultado = await _tools.ExecutarAsync(
                    "buscar_opcoes_agendamento",
                    (JsonObject)sessao.UltimaBuscaAgendamentoArgs.DeepClone(),
                    textoUsuario,
                    usuario,
                    cancellationToken);

                sessao.UltimoResultadoAgendamento =
                    (JsonObject)resultado.DeepClone();

                var respostaDireta =
                    FormatarResultadoReconsulta(resultado);

                sessao.Historico.Add(
                    ConteudoTexto("model", respostaDireta));

                ApararHistorico(sessao.Historico);
                LimparSessoesAntigas();

                return new AgenteResposta
                {
                    Resposta = respostaDireta,
                    SessionId = sid
                };
            }

            sessao.Historico.Add(
                ConteudoTexto("user", textoUsuario));

            ApararHistorico(sessao.Historico);

            var maxRounds = Math.Clamp(
                _options.MaxToolRounds,
                1,
                12);

            for (var round = 0; round < maxRounds; round++)
            {
                var request =
                    CriarRequest(sessao.Historico, usuario);

                var response =
                    await _gemini.GerarAsync(
                        request,
                        cancellationToken);

                var content =
                    ExtrairContent(response);

                if (content is null)
                {
                    var fallback =
                        "Não consegui interpretar a resposta do assistente. Tente novamente em instantes.";

                    sessao.Historico.Add(
                        ConteudoTexto("model", fallback));

                    return new AgenteResposta
                    {
                        Resposta = fallback,
                        SessionId = sid
                    };
                }

                sessao.Historico.Add(
                    (JsonObject)content.DeepClone());

                ApararHistorico(sessao.Historico);

                var calls =
                    ExtrairFunctionCalls(content);

                if (calls.Count == 0)
                {
                    var texto =
                        ExtrairTexto(content);

                    if (string.IsNullOrWhiteSpace(texto))
                        texto = GerarFallbackPeloEstado(sessao);

                    if (usuario.PrecisaIdentificarPaciente &&
                        PareceDeclararAlteracaoConcluida(texto))
                    {
                        texto = MensagemIdentificacaoObrigatoria();
                        LimparMutacaoPendente(sessao);
                    }

                    LimparSessoesAntigas();

                    return new AgenteResposta
                    {
                        Resposta = texto.Trim(),
                        SessionId = sid
                    };
                }

                var responseParts =
                    new JsonArray();
                string? respostaDiretaFerramenta = null;

                foreach (var call in calls)
                {
                    JsonObject resultado;

                    if (call.Nome == "encerrar_atendimento")
                    {
                        if (calls.Count == 1 && EncerramentoAtendimento.PodeSugerir(textoUsuario, sessao.MutacaoPendenteNome is not null))
                        {
                            sessao.Historico.Add(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(
                                new JsonObject { ["functionResponse"] = new JsonObject { ["name"] = "encerrar_atendimento", ["response"] = new JsonObject { ["sucesso"] = true } } }) });
                            sessao.Historico.Add(ConteudoTexto("model", EncerramentoAtendimento.Despedida));
                            ApararHistorico(sessao.Historico);
                            LimparSessoesAntigas();
                            return new AgenteResposta { SessionId = sid, Resposta = EncerramentoAtendimento.Despedida, EncerrarAtendimento = true };
                        }
                        resultado = new JsonObject { ["sucesso"] = false,
                            ["mensagem"] = "O atendimento deve continuar. Há pergunta/solicitação, agradecimento sem despedida ou confirmação pendente. Não peça avaliação; responda ao usuário ou conclua a confirmação." };
                    }
                    else if (EhFerramentaMutacao(call.Nome))
                    {
                        var chaveAtual = ChaveMutacao(call.Nome, call.Args, usuario);
                        var confirmouAgora = EhConfirmacaoExplicita(textoUsuario);

                        // Confirmação de presença é a própria intenção explícita do paciente
                        // (ex.: resposta CONFIRMAR a um lembrete). Não exigimos um segundo "sim".
                        if (call.Nome == "confirmar_consulta" && confirmouAgora)
                        {
                            resultado = await _tools.ExecutarAsync(
                                call.Nome, call.Args, textoUsuario, usuario, cancellationToken);
                            LimparMutacaoPendente(sessao);
                        }
                        else if (confirmouAgora)
                        {
                            var pendenciaValida =
                                !MutacaoPendenteExpirou(sessao) &&
                                string.Equals(sessao.MutacaoPendenteNome, call.Nome, StringComparison.Ordinal) &&
                                string.Equals(sessao.MutacaoPendenteChave, chaveAtual, StringComparison.Ordinal);

                            if (!pendenciaValida)
                            {
                                RegistrarMutacaoPendente(sessao, call, chaveAtual);
                                resultado = ConfirmacaoPendente(
                                    "Os dados da ação mudaram ou não havia uma ação preparada. " +
                                    "Apresente o resumo atual e peça uma nova confirmação.");
                            }
                            else
                            {
                                resultado = await _tools.ExecutarAsync(
                                    call.Nome,
                                    call.Args,
                                    textoUsuario,
                                    usuario,
                                    cancellationToken);

                                // Sucesso conclui a ação. Falha de negócio exige nova validação/resumo.
                                if (resultado["sucesso"]?.GetValue<bool?>() == true ||
                                    resultado["confirmacaoNecessaria"]?.GetValue<bool?>() != true)
                                {
                                    LimparMutacaoPendente(sessao);
                                }
                            }
                        }
                        else
                        {
                            // Primeira chamada prepara o payload, mas a Tool não altera nada sem "sim".
                            RegistrarMutacaoPendente(sessao, call, chaveAtual);
                            resultado = await _tools.ExecutarAsync(
                                call.Nome,
                                call.Args,
                                textoUsuario,
                                usuario,
                                cancellationToken);
                        }
                    }
                    else
                    {
                        resultado = await _tools.ExecutarAsync(
                            call.Nome,
                            call.Args,
                            textoUsuario,
                            usuario,
                            cancellationToken);
                    }

                    RegistrarEstadoFerramenta(
                        sessao,
                        call,
                        resultado);

                    responseParts.Add(
                        new JsonObject
                        {
                            ["functionResponse"] =
                                new JsonObject
                                {
                                    ["name"] = call.Nome,
                                    ["response"] =
                                        new JsonObject
                                        {
                                            ["result"] =
                                                resultado.DeepClone()
                                        }
                                }
                        });

                    respostaDiretaFerramenta =
                        CriarRespostaDiretaFerramenta(call.Nome, resultado);

                    if (!string.IsNullOrWhiteSpace(respostaDiretaFerramenta))
                        break;
                }

                sessao.Historico.Add(
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = responseParts
                    });

                if (!string.IsNullOrWhiteSpace(respostaDiretaFerramenta))
                {
                    sessao.Historico.Add(
                        ConteudoTexto("model", respostaDiretaFerramenta));

                    ApararHistorico(sessao.Historico);
                    LimparSessoesAntigas();

                    return new AgenteResposta
                    {
                        Resposta = respostaDiretaFerramenta,
                        SessionId = sid
                    };
                }

                ApararHistorico(sessao.Historico);
            }

            _logger.LogWarning(
                "Agente atingiu o limite de rodadas de ferramentas na sessão {SessionId}.",
                sid);

            return new AgenteResposta
            {
                Resposta = GerarFallbackPeloEstado(sessao),
                SessionId = sid
            };
        }
        finally
        {
            sessao.Lock.Release();
        }
    }

}
