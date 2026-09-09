using MKSANCrud.DTOs.Agente;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Agente;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Controllers;

[Authorize(Roles = "Paciente,Funcionario,Admin")]
public class AgenteController : Controller
{
    private const int MaxMensagem = 1000;
    private readonly IAgenteClinicaService _agente;
    private readonly UsuarioVinculoService _vinculos;
    private readonly AgenteHistoricoService _historico;
    private readonly ConvenioService _convenio;
    private readonly AtendimentoConversaService _atendimento;
    private readonly ILogger<AgenteController> _logger;

    public AgenteController(
        IAgenteClinicaService agente,
        UsuarioVinculoService vinculos,
        AgenteHistoricoService historico,
        ConvenioService convenio,
        AtendimentoConversaService atendimento,
        ILogger<AgenteController> logger)
    {
        _agente = agente;
        _vinculos = vinculos;
        _historico = historico;
        _convenio = convenio;
        _atendimento = atendimento;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("agente")]
    public async Task<IActionResult> Enviar(
        [FromBody] MensagemAgenteRequest request,
        CancellationToken cancellationToken)
    {
        var mensagem = (request.Mensagem ?? string.Empty).Trim();
        if (mensagem.Length == 0)
            return BadRequest(new { mensagem = "Digite uma mensagem." });

        if (mensagem.Length > MaxMensagem)
            return BadRequest(new { mensagem = $"A mensagem pode ter no máximo {MaxMensagem} caracteres." });

        var email = User.Identity?.Name;
        var podeGerenciar = User.IsInRole("Funcionario") || User.IsInRole("Admin");

        if (podeGerenciar)
        {
            var funcionario = await _vinculos.ObterFuncionarioAsync(User, cancellationToken);
            if (funcionario is null || !funcionario.Ativo)
                return Forbid();
        }

        var paciente = !podeGerenciar
            ? await _vinculos.ObterPacienteAsync(User, cancellationToken)
            : null;

        if (!podeGerenciar && (paciente is null || !paciente.Ativo))
            return Forbid();

        var usuario = new AgenteUsuarioContexto
        {
            Email = email,
            Telefone = paciente?.Telefone,
            Canal = "Site",
            PodeGerenciarOutrosPacientes = podeGerenciar,
            PacienteId = paciente?.Id,
            PacienteNome = paciente?.Nome,
            PacienteCpfMascarado = paciente is null ? null : MascararCpf(paciente.Cpf),
            PacienteDataNascimento = paciente?.DataNascimento,
            PacienteTemConvenio = paciente is null ? null : _convenio.EhValido(paciente),
            PacienteNomeConvenio = paciente is not null && _convenio.EhValido(paciente)
                ? paciente.NomeConvenio
                : null
        };

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var conversaAtendimento = await _atendimento.ObterOuCriarAsync(
            CanalAtendimento.Web,
            usuarioId,
            paciente?.Id,
            ct: cancellationToken);

        if (AtendimentoIntencao.EhPedidoAtendimentoHumano(
                mensagem))
        {
            await _atendimento.RegistrarEntradaAsync(
                conversaAtendimento,
                mensagem,
                ct: cancellationToken);

            await _atendimento.SolicitarAtendimentoHumanoAsync(
                conversaAtendimento,
                cancellationToken);

            const string respostaHumana =
                "Certo. Encaminhei sua conversa para a equipe CallMed. Um atendente poderá continuar por aqui.";

            var saida = await _atendimento.RegistrarSaidaAsync(
                conversaAtendimento,
                respostaHumana,
                AutorMensagemAtendimento.Sistema,
                StatusMensagemAtendimento.Enviada,
                ct: cancellationToken);

            return Json(new
            {
                resposta = respostaHumana,
                sessionId = conversaAtendimento.SessionId,
                modo = "Humano",
                conversaId = conversaAtendimento.Id,
                mensagemId = saida.Id
            });
        }

        // Se um funcionário assumiu a conversa, o site deixa de chamar a IA
        // e passa a funcionar como um chat humano com polling das respostas.
        if (conversaAtendimento.Modo == ModoAtendimento.Humano)
        {
            var entradaHumana = await _atendimento.RegistrarEntradaAsync(
                conversaAtendimento,
                mensagem,
                ct: cancellationToken);

            return Json(new
            {
                resposta = string.Empty,
                sessionId = conversaAtendimento.SessionId,
                modo = "Humano",
                conversaId = conversaAtendimento.Id,
                mensagemId = entradaHumana?.Id ?? 0
            });
        }

        IReadOnlyList<MensagemHistoricoAgente>? historico = null;

        // A Central Multicanal é a fonte preferida de continuidade porque
        // reúne Site, WhatsApp, SMS e e-mail do mesmo paciente.
        var historicoMulticanal =
            await _atendimento.CarregarHistoricoPacienteAsync(
                paciente?.Id,
                conversaAtendimento.Id,
                20,
                cancellationToken);

        if (historicoMulticanal.Count > 0)
            historico = historicoMulticanal;

        // Compatibilidade com conversas V12 anteriores à Central Multicanal.
        if ((historico is null || historico.Count == 0) &&
            !string.IsNullOrWhiteSpace(usuarioId) &&
            !string.IsNullOrWhiteSpace(request.SessionId))
        {
            var persistido = await _historico.CarregarAsync(
                usuarioId,
                request.SessionId,
                20,
                cancellationToken);

            if (persistido.Count > 0)
                historico = persistido;
        }

        historico ??= request.Historico?
            .Where(h => !string.IsNullOrWhiteSpace(h.Texto))
            .TakeLast(20)
            .Select(h => new MensagemHistoricoAgente
            {
                Papel = (h.Papel ?? string.Empty).Length > 20 ? h.Papel[..20] : h.Papel,
                Texto = h.Texto.Length > 700 ? h.Texto[..700] : h.Texto
            })
            .ToList();

        try
        {
            // A sessão persistida da Central é a autoridade. Isso impede
            // que uma confirmação antiga sobreviva a uma troca IA ↔ humano.
            var resposta = await _agente.EnviarAsync(
                mensagem,
                conversaAtendimento.SessionId,
                usuario,
                historico,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(usuarioId))
            {
                try
                {
                    await _historico.SalvarInteracaoAsync(
                        usuarioId,
                        resposta.SessionId,
                        mensagem,
                        resposta.Resposta,
                        cancellationToken);
                }
                catch (Exception exHistorico)
                {
                    // Falha de persistência não derruba o atendimento da IA.
                    _logger.LogWarning(exHistorico, "Não foi possível persistir o histórico do chat.");
                }
            }

            try
            {
                await _atendimento.RegistrarEntradaAsync(
                    conversaAtendimento,
                    mensagem,
                    ct: cancellationToken);

                await _atendimento.RegistrarSaidaAsync(
                    conversaAtendimento,
                    resposta.Resposta,
                    AutorMensagemAtendimento.Assistente,
                    StatusMensagemAtendimento.Enviada,
                    ct: cancellationToken);
            }
            catch (Exception exAtendimento)
            {
                _logger.LogWarning(
                    exAtendimento,
                    "Não foi possível persistir a conversa na Central de Atendimento.");
            }

            return Json(new
            {
                resposta = resposta.Resposta,
                sessionId = resposta.SessionId,
                modo = "IA",
                conversaId = conversaAtendimento.Id
            });
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("Gemini:ApiKey", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Gemini não configurado para o assistente CallMed.");
            return StatusCode(503, new { mensagem = "O assistente de IA ainda não foi configurado." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { mensagem = "O assistente demorou demais para responder. Tente novamente." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Falha na comunicação direta com o Gemini. HTTP {StatusCode}.",
                ex.StatusCode);

            var ehEquipe = User.IsInRole("Admin") || User.IsInRole("Funcionario");

            if (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var texto = ehEquipe
                    ? "A chave da IA foi recusada. Confira Gemini__ApiKey nas variáveis do Render."
                    : "O assistente está indisponível por uma configuração da IA. A equipe já pode verificar o acesso.";

                return StatusCode(503, new { mensagem = texto });
            }

            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var texto = ehEquipe
                    ? "O modelo de IA configurado não foi encontrado. Confira Gemini__Model no Render."
                    : "O assistente está indisponível por uma configuração da IA.";

                return StatusCode(503, new { mensagem = texto });
            }

            if (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return StatusCode(503, new
                {
                    mensagem = "O limite temporário da IA foi atingido. Aguarde alguns instantes e tente novamente."
                });
            }

            return StatusCode(502, new
            {
                mensagem = "Não foi possível conectar ao assistente agora. Tente novamente em alguns instantes."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no assistente CallMed.");
            return StatusCode(500, new { mensagem = "Não foi possível responder agora. Tente novamente." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> NovasMensagens(
        long afterId = 0,
        CancellationToken cancellationToken = default)
    {
        var usuarioId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(usuarioId))
            return Unauthorized();

        var podeGerenciar =
            User.IsInRole("Funcionario") ||
            User.IsInRole("Admin");

        var paciente = !podeGerenciar
            ? await _vinculos.ObterPacienteAsync(
                User,
                cancellationToken)
            : null;

        var conversa = await _atendimento.ObterOuCriarAsync(
            CanalAtendimento.Web,
            usuarioId,
            paciente?.Id,
            reativar: false,
            ct: cancellationToken);

        var mensagens =
            await _atendimento.CarregarSaidasHumanasDepoisAsync(
                conversa.Id,
                Math.Max(0, afterId),
                cancellationToken);

        return Json(new
        {
            modo = conversa.Modo.ToString(),
            conversaId = conversa.Id,
            sessionId = conversa.SessionId,
            mensagens = mensagens.Select(m => new
            {
                id = m.Id,
                texto = m.Texto,
                criadoEm = m.CriadoEm
            })
        });
    }

    private static string MascararCpf(string? cpf)
    {
        var numeros = new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        return numeros.Length == 11
            ? $"***.***.{numeros.Substring(6, 3)}-**"
            : "***";
    }
}
