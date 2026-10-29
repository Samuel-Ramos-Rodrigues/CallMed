using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.DTOs.Agente;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoOrquestradorService
{
    private readonly IAgenteClinicaService _agente;
    private readonly AtendimentoConversaService _conversas;
    private readonly AtendimentoEnvioService _envio;
    private readonly ConvenioService _convenio;
    private readonly SolicitacaoAtendimentoService _solicitacoes;
    private readonly ILogger<AtendimentoOrquestradorService> _logger;

    public AtendimentoOrquestradorService(
        IAgenteClinicaService agente,
        AtendimentoConversaService conversas,
        AtendimentoEnvioService envio,
        ConvenioService convenio,
        SolicitacaoAtendimentoService solicitacoes,
        ILogger<AtendimentoOrquestradorService> logger)
    {
        _agente = agente;
        _conversas = conversas;
        _envio = envio;
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
        var jaEstavaEmAtendimentoHumano = conversa.Modo == ModoAtendimento.Humano;

        // E-mail é um canal não autenticado: o histórico é restrito à própria conversa e
        // os dados cadastrais do paciente não são enviados ao modelo só por conhecer o remetente.
        var historico = await _conversas.CarregarHistoricoPacienteAsync(
            pacienteId: null,
            conversaAtualId: conversa.Id,
            limite: 16,
            ct: ct);

        var nova = await _conversas.RegistrarEntradaAsync(
            conversa,
            textoEntrada,
            entrada.MensagemExternaId,
            ct);
        if (nova is null) return; // ID externo já registrado: idempotência da leitura Gmail.

        // E-mail não autenticado não cria solicitações operacionais automaticamente.
        // Pedidos de agendamento/remarcação passam pela equipe, que pode verificar a identidade.

        // Conversa já assumida pela equipe: registrar a entrada, mas não disputar a resposta.
        if (jaEstavaEmAtendimentoHumano) return;

        // Pedidos de humano e conteúdo clínico/sensível não seguem para o modelo.
        // A mensagem fica visível para a equipe, sem resposta automática ou rascunho clínico.
        if (AtendimentoIntencao.EhPedidoAtendimentoHumano(textoEntrada) ||
            EmailRequerRevisaoHumana(textoEntrada))
        {
            await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
            return;
        }

        var usuario = new AgenteUsuarioContexto
        {
            Email = entrada.Identificador,
            Canal = "E-mail",
            PodeGerenciarOutrosPacientes = false,
            ConversaAtendimentoId = conversa.Id,
            // Não trate um remetente de e-mail como autenticado nem revele dados privados.
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
                textoEntrada,
                conversa.SessionId,
                usuario,
                historico,
                ct);

            if (string.IsNullOrWhiteSpace(resposta.Resposta))
            {
                await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
                return;
            }

            await _conversas.RegistrarSaidaAsync(
                conversa,
                resposta.Resposta,
                AutorMensagemAtendimento.Assistente,
                StatusMensagemAtendimento.AguardandoAprovacao,
                erro: "Rascunho gerado pela IA. Revise antes de enviar ao paciente.",
                ct: ct);
            await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível gerar um rascunho de e-mail para a conversa {ConversaId}; a equipe responderá manualmente.", conversa.Id);
            await _conversas.SolicitarAtendimentoHumanoAsync(conversa, ct);
        }
    }

    private static bool EmailRequerRevisaoHumana(string texto)
    {
        var semAcentos = NormalizarSemAcentos(texto);

        // Não enviar conteúdo clínico ou identificadores pessoais ao modelo por um canal não autenticado.
        var termosSensíveis = new[]
        {
            "sintoma", "sinto dor", "estou com dor", "dor no peito", "falta de ar", "dificuldade para respirar",
            "sangramento", "febre", "urgente", "emergencia", "laudo", "resultado de exame", "resultado do exame",
            "exame", "medicamento", "remedio", "receita medica", "prescricao", "diagnostico", "tratamento",
            "doenca", "gravidez", "gestante", "pressao arterial", "glicemia", "efeito colateral", "consulta",
            "convenio", "carteirinha", "cpf", "data de nascimento", "minha senha e", "senha e", "codigo de acesso",
            "prontuario", "numero do documento", "meu documento"
        };
        if (termosSensíveis.Any(termo => semAcentos.Contains(NormalizarSemAcentos(termo), StringComparison.Ordinal)))
            return true;

        if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"(?<!\d)\d{11}(?!\d)"))
            return true;

        // Lista positiva: somente dúvidas administrativas genéricas e previsíveis podem gerar rascunho da IA.
        // Todo pedido de ação, conteúdo desconhecido ou mensagem ambígua segue para a equipe humana.
        var temasAdministrativosPermitidos = new[]
        {
            "horario de funcionamento", "horario de atendimento", "qual o horario", "quais os horarios",
            "que horas abre", "que horas fecha", "a que horas abre", "a que horas fecha", "funcionamento da clinica",
            "endereco da clinica", "qual o endereco", "onde fica", "localizacao da clinica", "como chegar",
            "telefone de contato", "qual o telefone", "informacao de contato", "informacoes de contato",
            "email de contato", "whatsapp da clinica", "canal de contato",
            "como funciona o callmed", "como funciona a callmed", "como usar o site", "como usar o portal",
            "como acessar o portal", "como acessar minha conta", "nao consigo acessar minha conta",
            "nao consigo entrar no portal", "recuperar senha", "esqueci minha senha", "esqueci a senha",
            "como criar uma conta", "como me cadastrar", "como fazer cadastro"
        };
        return !temasAdministrativosPermitidos.Any(tema => semAcentos.Contains(NormalizarSemAcentos(tema), StringComparison.Ordinal));
    }

    private static string NormalizarSemAcentos(string texto)
    {
        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var chars = decomposto.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                                          System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
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

        var paciente = conversa.Paciente;

        var usuario = new AgenteUsuarioContexto
        {
            Email = paciente?.Email ??
                    (entrada.Canal == CanalAtendimento.Email
                        ? conversa.IdentificadorExterno
                        : null),
            Telefone = paciente?.Telefone ??
                       (entrada.Canal is CanalAtendimento.WhatsApp or CanalAtendimento.Sms
                           ? conversa.IdentificadorExterno
                           : null),
            Canal = NomeCanal(entrada.Canal),
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

        try
        {
            var resposta = await _agente.EnviarAsync(
                textoEntrada,
                conversa.SessionId,
                usuario,
                historico,
                ct);

            await _envio.EnviarAsync(
                conversa,
                resposta.Resposta,
                AutorMensagemAtendimento.Assistente,
                assunto: entrada.Assunto,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao processar atendimento {Canal}, conversa {ConversaId}.",
                entrada.Canal,
                conversa.Id);

            await _envio.EnviarAsync(
                conversa,
                "Não consegui concluir o atendimento agora. Tente novamente em alguns instantes.",
                AutorMensagemAtendimento.Sistema,
                assunto: entrada.Assunto,
                ct: ct);
        }
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

        if (canal == CanalAtendimento.Email)
        {
            // Remove citações simples de respostas anteriores para não mandar
            // a thread inteira novamente ao modelo.
            var linhas = valor.Split('\n');
            var filtradas = new List<string>();

            foreach (var linha in linhas)
            {
                var l = linha.Trim();

                if (l.StartsWith('>'))
                    continue;

                if (l.StartsWith("Em ", StringComparison.OrdinalIgnoreCase) &&
                    l.Contains("escreveu", StringComparison.OrdinalIgnoreCase))
                    break;

                if (l.StartsWith("On ", StringComparison.OrdinalIgnoreCase) &&
                    l.Contains("wrote", StringComparison.OrdinalIgnoreCase))
                    break;

                filtradas.Add(linha);
            }

            valor = string.Join("\n", filtradas).Trim();
        }

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
