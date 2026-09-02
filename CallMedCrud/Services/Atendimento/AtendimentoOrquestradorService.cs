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

        var conversa = await _conversas.ObterOuCriarAsync(
            entrada.Canal,
            entrada.Identificador,
            assunto: entrada.Assunto,
            ct: ct);

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
        // Assim WhatsApp, e-mail, SMS e PWA entram no mesmo pipeline de triagem.
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
