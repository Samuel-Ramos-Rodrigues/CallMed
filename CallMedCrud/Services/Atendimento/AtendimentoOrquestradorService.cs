using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.DTOs.Agente;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoOrquestradorService
{
    private static readonly SemaphoreSlim[] TravasRetomada = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public Task<ResultadoRetomadaAtendimento> DevolverParaIaERetomarAsync(ConversaAtendimento conversa, CancellationToken ct = default) =>
        RetomarComExclusaoAsync(conversa, devolver: true, ct);

    public Task<ResultadoRetomadaAtendimento> RetomarUltimaEntradaPendenteAsync(ConversaAtendimento conversa, CancellationToken ct = default) =>
        RetomarComExclusaoAsync(conversa, devolver: false, ct);

    public Task<ResultadoRetomadaAtendimento> GerarRascunhoEmailAsync(ConversaAtendimento conversa, CancellationToken ct = default) =>
        RetomarComExclusaoAsync(conversa, devolver: false, ct, rascunhoManual: true);

    private async Task<ResultadoRetomadaAtendimento> RetomarComExclusaoAsync(ConversaAtendimento conversa, bool devolver, CancellationToken ct, bool rascunhoManual = false)
    {
        var trava = TravasRetomada[(int)(conversa.Id % TravasRetomada.Length)];
        await trava.WaitAsync(ct);
        try
        {
            // A conversa pode ter sido assumida/encerrada enquanto aguardávamos a trava.
            await _conversas.RecarregarAsync(conversa, ct);
            if (devolver) await _conversas.DevolverParaIaAsync(conversa, ct);
            if (!conversa.Ativa || (conversa.Modo != ModoAtendimento.IA && !rascunhoManual))
                return ResultadoRetomadaAtendimento.SemMensagemPendente;
            return conversa.Canal == CanalAtendimento.Email
                ? await RetomarUltimoEmailPendenteAsync(conversa, ct, rascunhoManual)
                : await RetomarCanalConversacionalAsync(conversa, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (conversa.IaEntradaId is long entradaId && conversa.IaEstado is "analisando" or "gerando")
                await _conversas.AtualizarEstadoIaAsync(conversa, entradaId, "falha", CancellationToken.None);
            throw;
        }
        finally { trava.Release(); }
    }

    private async Task<ResultadoRetomadaAtendimento> RetomarCanalConversacionalAsync(ConversaAtendimento conversa, CancellationToken ct)
    {
        var ultima = await _conversas.ObterUltimaEntradaPendenteAsync(conversa.Id, ct);
        if (ultima is null) return ResultadoRetomadaAtendimento.SemMensagemPendente;
        await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "analisando", ct);
        if (AtendimentoIntencao.EhPedidoAtendimentoHumano(ultima.Texto))
        {
            await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
            await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "equipe", ct);
            return ResultadoRetomadaAtendimento.RequerEquipe;
        }
        conversa = await _conversas.ObterPorIdAsync(conversa.Id, ct) ?? conversa;
        var historico = await _conversas.CarregarHistoricoPacienteAsync(conversa.PacienteId, conversa.Id, 24, ct, excluirMensagemId: ultima.Id);
        try
        {
            await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "gerando", ct);
            var resposta = await _agente.EnviarAsync(NormalizarTextoEntrada(ultima.Texto, conversa.Canal), conversa.SessionId, CriarContextoUsuario(conversa), historico, ct);
            if (string.IsNullOrWhiteSpace(resposta.Resposta))
            {
                await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
                await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "falha", ct);
                return ResultadoRetomadaAtendimento.FalhaNaGeracao;
            }
            var saida = await _envio.EnviarRespostaIaAsync(conversa, resposta.Resposta, ultima.Id, ct);
            await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "concluida", ct);
            return saida.Status == StatusMensagemAtendimento.Enviada ? ResultadoRetomadaAtendimento.RespostaEnviada
                : saida.Status == StatusMensagemAtendimento.Processando ? ResultadoRetomadaAtendimento.RespostaEmProcessamento
                : ResultadoRetomadaAtendimento.FalhaNoEnvio;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao retomar IA na conversa {ConversaId}.", conversa.Id);
            await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
            await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "falha", ct);
            return ResultadoRetomadaAtendimento.FalhaNaGeracao;
        }
    }

    private readonly IAgenteClinicaService _agente;
    private readonly AtendimentoConversaService _conversas;
    private readonly AtendimentoEnvioService _envio;
    private readonly AtendimentoEmailDecisaoService _decisaoEmail;
    private readonly ConvenioService _convenio;
    private readonly SolicitacaoAtendimentoService _solicitacoes;
    private readonly ILogger<AtendimentoOrquestradorService> _logger;

    public AtendimentoOrquestradorService(
        IAgenteClinicaService agente,
        AtendimentoConversaService conversas,
        AtendimentoEnvioService envio,
        AtendimentoEmailDecisaoService decisaoEmail,
        ConvenioService convenio,
        SolicitacaoAtendimentoService solicitacoes,
        ILogger<AtendimentoOrquestradorService> logger)
    {
        _agente = agente;
        _conversas = conversas;
        _envio = envio;
        _decisaoEmail = decisaoEmail;
        _convenio = convenio;
        _solicitacoes = solicitacoes;
        _logger = logger;
    }

    /// <summary>
    /// Trata uma mensagem recebida pela Gmail API e salva uma sugestão da IA para revisão.
    /// Diferente dos webhooks conversacionais, este fluxo nunca envia automaticamente a resposta.
    /// </summary>
    public async Task ProcessarEntradaEmailComoRascunhoAsync(
        CanalMensagemEntrada entrada,
        CancellationToken ct = default)
    {
        if (entrada.Canal != CanalAtendimento.Email ||
            string.IsNullOrWhiteSpace(entrada.Identificador) ||
            string.IsNullOrWhiteSpace(entrada.Texto))
            return;

        var textoEntrada = NormalizarTextoEntrada(entrada.Texto, CanalAtendimento.Email);
        if (string.IsNullOrWhiteSpace(textoEntrada)) return;

        var conversa = await _conversas.ObterOuCriarAsync(
            CanalAtendimento.Email,
            entrada.Identificador,
            assunto: entrada.Assunto,
            ct: ct,
            // Um remetente de e-mail, por si só, não deve vincular a conversa a um prontuário.
            resolverIdentidade: false);
        var nova = await _conversas.RegistrarEntradaAsync(
            conversa, textoEntrada, entrada.MensagemExternaId, ct);
        if (nova is null) return; // Releitura Gmail não gera nova análise.
        if (conversa.Modo == ModoAtendimento.Humano) return;

        // O caminho automático e os botões usam a mesma exclusão e as mesmas regras.
        await RetomarUltimaEntradaPendenteAsync(conversa, ct);
    }

    /// <summary>
    /// Ao clicar "Devolver para IA", tenta preparar um rascunho para a última
    /// entrada que chegou no modo humano e ainda não possui resposta. Não
    /// reimporta a mensagem do Gmail e nunca envia e-mail sem aprovação.
    /// </summary>
    private async Task<ResultadoRetomadaAtendimento> RetomarUltimoEmailPendenteAsync(
        ConversaAtendimento conversa,
        CancellationToken ct = default,
        bool rascunhoManual = false)
    {
        if (conversa.Canal != CanalAtendimento.Email || (conversa.Modo != ModoAtendimento.IA && !rascunhoManual))
            return ResultadoRetomadaAtendimento.SemMensagemPendente;

        var ultima = await _conversas.ObterUltimaEntradaPendenteAsync(conversa.Id, ct);
        if (ultima is null)
            return ResultadoRetomadaAtendimento.SemMensagemPendente;

        await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "analisando", ct);
        var texto = NormalizarTextoEntrada(ultima.Texto, CanalAtendimento.Email);
        var historico = await _conversas.CarregarHistoricoEmailAsync(conversa.Id, ultima.Id, ct);
        var decisao = await _decisaoEmail.AnalisarAsync(texto, ct, historico);
        if (decisao != DecisaoEmail.PrepararRascunho)
        {
            _logger.LogInformation("Retomada de e-mail {ConversaId} interrompida. Decisao={Decisao}", conversa.Id, decisao);
            if (conversa.Modo == ModoAtendimento.IA)
                await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
            await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, decisao == DecisaoEmail.Equipe ? "equipe" : "falha", ct);
            return decisao == DecisaoEmail.Equipe
                ? ResultadoRetomadaAtendimento.RequerEquipe
                : ResultadoRetomadaAtendimento.FalhaNaGeracao;
        }

        await _conversas.AtualizarEstadoIaAsync(conversa, ultima.Id, "gerando", ct);
        return await CriarRascunhoEmailAsync(conversa, texto, ultima.Id, historico, ct);
    }

    private async Task<ResultadoRetomadaAtendimento> CriarRascunhoEmailAsync(
        ConversaAtendimento conversa,
        string textoEntrada,
        long entradaId,
        IReadOnlyList<MensagemHistoricoAgente> historico,
        CancellationToken ct)
    {
        var sessaoInicial = conversa.SessionId;
        var usuario = new AgenteUsuarioContexto
        {
            Email = conversa.IdentificadorExterno,
            Canal = "E-mail",
            PodeGerenciarOutrosPacientes = false,
            ConversaAtendimentoId = conversa.Id,
            // E-mail não é prova de identidade, mesmo se a conversa estiver
            // vinculada manualmente a um paciente.
            PacienteId = null,
            PacienteNome = null,
            PacienteCpfMascarado = null,
            PacienteDataNascimento = null,
            PacienteTemConvenio = null,
            PacienteNomeConvenio = null
        };

        try
        {
            var resposta = await _agente.EnviarAsync(
                AtendimentoEmailPolitica.OmitirTelefones(textoEntrada),
                conversa.SessionId,
                usuario,
                historico,
                ct);

            if (string.IsNullOrWhiteSpace(resposta.Resposta)) throw new InvalidOperationException("Resposta da IA vazia.");

            await _conversas.RecarregarAsync(conversa, ct);
            if (!conversa.Ativa || conversa.SessionId != sessaoInicial)
            {
                await _conversas.AtualizarEstadoIaAsync(conversa, entradaId, "equipe", ct);
                return ResultadoRetomadaAtendimento.RequerEquipe;
            }
            // Uma nova entrada pode chegar durante a geração. Não trate a nova pergunta
            // como respondida por um rascunho que foi preparado para a mensagem anterior.
            var pendente = await _conversas.ObterUltimaEntradaPendenteAsync(conversa.Id, ct);
            if (pendente?.Id != entradaId) return ResultadoRetomadaAtendimento.SemMensagemPendente;
            var chave = await _conversas.ChaveRascunhoAsync(conversa.Id, entradaId, ct);
            await _conversas.RegistrarSaidaAsync(
                conversa,
                resposta.Resposta,
                AutorMensagemAtendimento.Assistente,
                StatusMensagemAtendimento.AguardandoAprovacao,
                erro: "Rascunho gerado pela IA. Revise antes de enviar ao paciente.",
                ct: ct,
                chaveEnvio: chave);

            await _conversas.AtualizarEstadoIaAsync(conversa, entradaId, "pronto", ct);
            // Gerar manualmente não muda o responsável nem envia o e-mail.
            return ResultadoRetomadaAtendimento.RascunhoCriado;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao gerar rascunho de e-mail para a conversa {ConversaId}.",
                conversa.Id);
            await _conversas.RecarregarAsync(conversa, ct);
            if (conversa.Ativa && conversa.SessionId == sessaoInicial && conversa.Modo == ModoAtendimento.IA)
                await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
            await _conversas.AtualizarEstadoIaAsync(conversa, entradaId, "falha", ct);
            return ResultadoRetomadaAtendimento.FalhaNaGeracao;
        }
    }

    public async Task ProcessarEntradaAsync(
        CanalMensagemEntrada entrada,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entrada.Identificador) ||
            string.IsNullOrWhiteSpace(entrada.Texto))
            return;

        // Limite server-side: webhooks externos não podem forçar prompts
        // arbitrariamente grandes ou armazenar mensagens ilimitadas.
        var textoEntrada = NormalizarTextoEntrada(
            entrada.Texto,
            entrada.Canal);

        if (string.IsNullOrWhiteSpace(textoEntrada))
            return;

        ConversaAtendimento conversa;
        if (entrada.ConversaAtendimentoId is long conversaId)
        {
            // Uma resposta de e-mail roteada por alias assinado só pode entrar
            // na conversa correspondente e se vier do mesmo remetente original.
            var existente = await _conversas.ObterPorIdAsync(conversaId, ct);
            if (existente is null || existente.Canal != entrada.Canal)
            {
                _logger.LogWarning("Mensagem recebida para conversa inexistente ou canal incorreto. ConversaId={ConversaId}.", conversaId);
                return;
            }

            var identificadorEsperado = AtendimentoIdentidadeService.NormalizarIdentificador(
                existente.Canal, existente.IdentificadorExterno);
            var identificadorRecebido = AtendimentoIdentidadeService.NormalizarIdentificador(
                entrada.Canal, entrada.Identificador);
            if (!string.Equals(identificadorEsperado, identificadorRecebido, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Resposta de e-mail ignorada porque o remetente não corresponde à conversa {ConversaId}.", conversaId);
                return;
            }

            conversa = existente;
        }
        else
        {
            conversa = await _conversas.ObterOuCriarAsync(
                entrada.Canal,
                entrada.Identificador,
                assunto: entrada.Assunto,
                ct: ct);
        }

        var historico = await _conversas.CarregarHistoricoPacienteAsync(
            conversa.PacienteId,
            conversa.Id,
            24,
            ct);

        var nova = await _conversas.RegistrarEntradaAsync(
            conversa,
            textoEntrada,
            entrada.MensagemExternaId,
            ct);

        if (nova is null)
            return;

        // Todo pedido de marcação/remarcação vira também uma solicitação operacional.
        // Assim WhatsApp, e-mail, SMS e PWA entram no mesmo fluxo de solicitações e agendamento.
        if (AtendimentoIntencao.EhPedidoAgendamento(textoEntrada))
        {
            await _solicitacoes.ObterOuCriarDaConversaAsync(
                conversa,
                textoEntrada,
                ct);
        }

        if (AtendimentoIntencao.EhPedidoAtendimentoHumano(
                textoEntrada))
        {
            await _conversas.SolicitarAtendimentoHumanoAsync(
                conversa,
                ct);

            await _envio.EnviarAsync(
                conversa,
                "Certo. Encaminhei sua conversa para a equipe CallMed. Um atendente poderá continuar por este mesmo canal.",
                AutorMensagemAtendimento.Sistema,
                assunto: entrada.Assunto,
                ct: ct);

            return;
        }

        if (conversa.Modo == ModoAtendimento.Humano)
            return;

        var usuario = CriarContextoUsuario(conversa);
        await _conversas.AtualizarEstadoIaAsync(conversa, nova.Id, "gerando", ct);

        try
        {
            var resposta = await _agente.EnviarAsync(
                textoEntrada,
                conversa.SessionId,
                usuario,
                historico,
                ct);

            if (entrada.Canal == CanalAtendimento.Email)
                await _envio.EnviarAsync(conversa, resposta.Resposta, AutorMensagemAtendimento.Assistente, assunto: entrada.Assunto, ct: ct);
            else
                await _envio.EnviarRespostaIaAsync(conversa, resposta.Resposta, nova.Id, ct);
            await _conversas.AtualizarEstadoIaAsync(conversa, nova.Id, "concluida", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await _conversas.AtualizarEstadoIaAsync(conversa, nova.Id, "falha", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao processar atendimento {Canal}, conversa {ConversaId}.",
                entrada.Canal,
                conversa.Id);
            await _conversas.AtualizarEstadoIaAsync(conversa, nova.Id, "falha", ct);

            await _envio.EnviarAsync(
                conversa,
                "Não consegui concluir o atendimento agora. Tente novamente em alguns instantes.",
                AutorMensagemAtendimento.Sistema,
                assunto: entrada.Assunto,
                ct: ct);
        }
    }

    private AgenteUsuarioContexto CriarContextoUsuario(ConversaAtendimento conversa)
    {
        var paciente = conversa.Paciente;

        return new AgenteUsuarioContexto
        {
            Email = paciente?.Email ??
                    (conversa.Canal == CanalAtendimento.Email
                        ? conversa.IdentificadorExterno
                        : null),
            Telefone = paciente?.Telefone ??
                       (conversa.Canal is CanalAtendimento.WhatsApp or CanalAtendimento.Sms
                           ? conversa.IdentificadorExterno
                           : null),
            Canal = NomeCanal(conversa.Canal),
            PodeGerenciarOutrosPacientes = false,
            ConversaAtendimentoId = conversa.Id,
            PacienteId = paciente?.Id,
            PacienteNome = paciente?.Nome,
            PacienteCpfMascarado = paciente is null
                ? null
                : MascararCpf(paciente.Cpf),
            PacienteDataNascimento = paciente?.DataNascimento,
            PacienteTemConvenio = paciente is null
                ? null
                : _convenio.EhValido(paciente),
            PacienteNomeConvenio =
                paciente is not null && _convenio.EhValido(paciente)
                    ? paciente.NomeConvenio
                    : null
        };
    }

    private static string NormalizarTextoEntrada(
        string texto,
        CanalAtendimento canal)
    {
        var valor = texto
            .Replace("\0", string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();

        if (canal == CanalAtendimento.Email) return AtendimentoEmailTexto.Normalizar(valor);

        const int limite = 3000;
        return valor.Length <= limite
            ? valor
            : valor[..limite];
    }

    private static string NomeCanal(CanalAtendimento canal) =>
        canal switch
        {
            CanalAtendimento.WhatsApp => "WhatsApp",
            CanalAtendimento.Sms => "SMS",
            CanalAtendimento.Email => "E-mail",
            _ => "Site"
        };

    private static string MascararCpf(string? cpf)
    {
        var numeros = new string(
            (cpf ?? string.Empty)
                .Where(char.IsDigit)
                .ToArray());

        return numeros.Length == 11
            ? $"***.***.{numeros.Substring(6, 3)}-**"
            : "***";
    }
}
