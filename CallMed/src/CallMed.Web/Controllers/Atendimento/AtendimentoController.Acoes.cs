using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.ViewModels;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Usuarios;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

public sealed partial class AtendimentoController : Controller
{

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assumir(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
            return Forbid();

        await _conversas.AssumirAsync(
            conversa,
            usuarioId,
            ct);

        TempData["Sucesso"] = "Atendimento assumido por você.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevolverParaIa(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var resultado = await _orquestrador.DevolverParaIaERetomarAsync(conversa, ct);
        AplicarResultadoRetomada(resultado);
        return await ResponderAcaoIaAsync(conversa, resultado, ct);
    }

    /// <summary>
    /// Permite tentar novamente a análise de uma entrada de e-mail pendente mesmo
    /// em conversas antigas, inclusive assumidas pela equipe. Não envia e-mail nem muda o responsável.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GerarRascunhoPendente(long id, CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct)) return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conversa is null) return NotFound();
        if (!conversa.Ativa || conversa.Canal != CanalAtendimento.Email)
        {
            TempData["Erro"] = "Abra uma conversa de e-mail para gerar um rascunho.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var resultado = await _orquestrador.GerarRascunhoEmailAsync(conversa, ct);
        if (resultado == ResultadoRetomadaAtendimento.SemMensagemPendente)
        {
            var estado = await _conversas.ObterEstadoIaAsync(conversa, ct);
            TempData["Aviso"] = $"{estado.Titulo}. {estado.Detalhe}";
        }
        else AplicarResultadoRetomada(resultado);
        return await ResponderAcaoIaAsync(conversa, resultado, ct);
    }

    private async Task<IActionResult> ResponderAcaoIaAsync(ConversaAtendimento conversa, ResultadoRetomadaAtendimento resultado, CancellationToken ct)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // O cliente mantém a conversa e o progresso visíveis durante o POST.
            return Json(new { resultado = resultado.ToString(), ia = await _conversas.ObterEstadoIaAsync(conversa, ct) });
        }
        return RedirectToAction(nameof(Index), new { id = conversa.Id });
    }

    private void AplicarResultadoRetomada(ResultadoRetomadaAtendimento resultado)
    {
        switch (resultado)
        {
            case ResultadoRetomadaAtendimento.RascunhoCriado:
                TempData["Sucesso"] = "A IA criou um rascunho para a última mensagem. Confira o histórico e aprove o envio quando estiver correto. Nenhum e-mail foi enviado automaticamente.";
                break;
            case ResultadoRetomadaAtendimento.RespostaEnviada:
                TempData["Sucesso"] = "A IA retomou a mensagem pendente e enviou a resposta pelo canal da conversa.";
                break;
            case ResultadoRetomadaAtendimento.RespostaEmProcessamento:
                TempData["Sucesso"] = "A resposta da IA já está em processamento no canal. Não é necessário devolver a conversa novamente.";
                break;
            case ResultadoRetomadaAtendimento.FalhaNoEnvio:
                TempData["Erro"] = "A IA preparou a resposta, mas o canal não confirmou o envio. Confira a mensagem e o estado do canal antes de tentar reenviar.";
                break;
            case ResultadoRetomadaAtendimento.AtendimentoEncerrado:
                TempData["Sucesso"] = "Atendimento encerrado. A avaliação será oferecida pelo próprio canal.";
                break;
            case ResultadoRetomadaAtendimento.RequerEquipe:
                TempData["Aviso"] = "A mensagem exige atendimento humano por envolver identificação, dados individuais, assunto clínico ou pedido de um atendente. O histórico foi preservado; nenhuma resposta foi enviada automaticamente.";
                break;
            case ResultadoRetomadaAtendimento.FalhaNaGeracao:
                TempData["Erro"] = "Não foi possível preparar a resposta da IA. Confira o indicador para acompanhar a próxima tentativa ou solicitar nova análise.";
                break;
            default:
                TempData["Sucesso"] = "A IA está ativa e não há mensagem pendente sem resposta. Rascunhos manuais continuam aguardando revisão; as próximas mensagens seguem o modo configurado do canal.";
                break;
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Encerrar(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        if (_feedback is not null) await _feedback.EncerrarAsync(conversa, ct);
        else await _conversas.EncerrarAsync(conversa, ct);

        TempData["Sucesso"] = "Conversa encerrada.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VincularPaciente(
        long id,
        int pacienteId,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var vinculado = await _conversas.VincularPacienteAsync(
            conversa,
            pacienteId,
            ct);

        TempData[vinculado ? "Sucesso" : "Erro"] = vinculado
            ? "Contato vinculado ao paciente. O contexto seguro da conversa foi renovado."
            : "Não foi possível vincular esse paciente à conversa.";

        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesvincularPaciente(
        long id,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var desvinculado = await _conversas.DesvincularPacienteAsync(
            conversa,
            ct);

        TempData[desvinculado ? "Sucesso" : "Erro"] = desvinculado
            ? "Contato desvinculado. A próxima mensagem será reidentificada pelo canal."
            : "Conversas do Site/PWA não podem ser desvinculadas manualmente.";

        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AprovarRascunho(long mensagemId, CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct)) return Forbid();
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId)) return Forbid();

        var rascunho = await _envio.AprovarRascunhoAsync(mensagemId, usuarioId, ct);
        if (rascunho?.Conversa is null)
        {
            TempData["Erro"] = "Este rascunho já foi tratado ou não está disponível para aprovação.";
            return RedirectToAction(nameof(Index));
        }

        TempData[rascunho.Status == StatusMensagemAtendimento.Enviada ? "Sucesso" : "Erro"] =
            rascunho.Status == StatusMensagemAtendimento.Enviada
                ? "Rascunho aprovado e enviado ao paciente."
                : rascunho.Erro ?? "Não foi possível enviar o rascunho. Revise o canal de e-mail.";
        return RedirectToAction(nameof(Index), new { id = rascunho.ConversaAtendimentoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DescartarRascunho(long mensagemId, CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct)) return Forbid();
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId)) return Forbid();

        var mensagem = await _context.MensagensAtendimento.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mensagemId, ct);
        if (mensagem is null) return NotFound();

        var descartado = await _envio.DescartarRascunhoAsync(mensagemId, usuarioId, ct);
        TempData[descartado ? "Sucesso" : "Erro"] = descartado
            ? "Rascunho descartado. Nenhum e-mail foi enviado."
            : "O rascunho já foi tratado ou não está mais disponível.";
        return RedirectToAction(nameof(Index), new { id = mensagem.ConversaAtendimentoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(
        long mensagemId,
        CancellationToken ct)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var falha = await _context.MensagensAtendimento
            .Include(m => m.Conversa)
            .FirstOrDefaultAsync(m =>
                m.Id == mensagemId &&
                m.Direcao == DirecaoMensagemAtendimento.Saida,
                ct);

        if (falha?.Conversa is null)
            return NotFound();

        var atual = await _envio.ReenviarAsync(mensagemId, ct);
        if (atual is null) return NotFound();
        var enviada = atual.Status is StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue or StatusMensagemAtendimento.AguardandoEnvio;
        TempData[enviada ? "Sucesso" : "Erro"] = enviada
            ? atual.Status == StatusMensagemAtendimento.AguardandoEnvio ? "Mensagem na fila de SMS. Aguarde o envio pelo aparelho." : "Mensagem enviada."
            : atual.Status == StatusMensagemAtendimento.Processando
                ? "Esta mensagem já está em processamento. Aguarde a confirmação."
                : atual.Erro ?? "Aguarde antes de tentar novamente.";

        return RedirectToAction(
            nameof(Index),
            new { id = falha.ConversaAtendimentoId });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Atualizacoes(
        long id,
        long afterId = 0,
        CancellationToken ct = default)
    {
        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var mensagens = await _context.MensagensAtendimento
            .AsNoTracking()
            .Where(m =>
                m.ConversaAtendimentoId == id &&
                m.Id > Math.Max(0, afterId))
            .OrderBy(m => m.Id)
            .Take(80)
            .Select(m => new
            {
                id = m.Id,
                direcao = m.Direcao.ToString(),
                autor = m.Autor.ToString(),
                texto = m.Texto,
                status = m.Status.ToString(),
                erro = m.Erro,
                proximaTentativaEm = m.ProximaTentativaEm,
                reenvioBloqueado = m.ReenvioBloqueado,
                criadoEm = m.CriadoEm
            })
            .ToListAsync(ct);

        var estados = await _context.MensagensAtendimento.AsNoTracking()
            .Where(m => m.ConversaAtendimentoId == id && m.Id <= Math.Max(0, afterId) &&
                m.Direcao == DirecaoMensagemAtendimento.Saida)
            .OrderByDescending(m => m.Id).Take(200)
            .Select(m => new { id = m.Id, status = m.Status.ToString(), erro = m.Erro,
                proximaTentativaEm = m.ProximaTentativaEm, reenvioBloqueado = m.ReenvioBloqueado })
            .ToListAsync(ct);

        if (mensagens.Count > 0)
        {
            var agora = DateTime.UtcNow;
            await _context.ConversasAtendimento
                .Where(c => c.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(c => c.VisualizadaEm, agora),
                    ct);
        }

        return Json(new
        {
            conversaId = conversa.Id,
            modo = conversa.Modo.ToString(),
            ativa = conversa.Ativa,
            responsavelUsuarioId = conversa.ResponsavelUsuarioId,
            pacienteId = conversa.PacienteId,
            ia = await _conversas.ObterEstadoIaAsync(conversa, ct),
            estados,
            mensagens
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarMensagem(
        long id,
        string mensagem,
        CancellationToken ct)
    {
        var texto = (mensagem ?? string.Empty).Trim();

        if (texto.Length == 0)
        {
            TempData["Erro"] = "Digite uma mensagem.";
            return RedirectToAction(nameof(Index), new { id });
        }

        if (texto.Length > 4000)
        {
            TempData["Erro"] = "A mensagem pode ter no máximo 4.000 caracteres.";
            return RedirectToAction(nameof(Index), new { id });
        }

        if (!await PodeOperarAtendimentoAsync(ct))
            return Forbid();

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversa is null)
            return NotFound();

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (conversa.Modo != ModoAtendimento.Humano ||
            !string.Equals(
                conversa.ResponsavelUsuarioId,
                userId,
                StringComparison.Ordinal))
        {
            await _conversas.AssumirAsync(
                conversa,
                userId,
                ct);
        }

        var saida = await _envio.EnviarAsync(
            conversa,
            texto,
            AutorMensagemAtendimento.Funcionario,
            userId,
            ct: ct);

        if (saida.Status == StatusMensagemAtendimento.AguardandoEnvio)
            TempData["Sucesso"] = "Mensagem na fila de SMS. Aguarde o envio pelo aparelho.";
        else if (saida.Status is StatusMensagemAtendimento.Enviada or StatusMensagemAtendimento.Entregue)
            TempData["Sucesso"] = "Mensagem enviada.";
        else
            TempData["Erro"] =
                saida.Erro ??
                "Não foi possível enviar a mensagem.";

        return RedirectToAction(nameof(Index), new { id });
    }
    private async Task<bool> PodeOperarAtendimentoAsync(CancellationToken ct)
    {
        // Admin pode operar a Central mesmo sem um cadastro espelhado em Funcionarios.
        // Para Funcionario, o vínculo precisa existir e estar ativo.
        if (User.IsInRole("Admin"))
            return true;

        var funcionario = await _vinculos.ObterFuncionarioAsync(User, ct);
        return funcionario is { Ativo: true };
    }

    private async Task<IReadOnlyList<PacienteOpcaoAtendimentoViewModel>>
        CarregarPacientesAsync(CancellationToken ct)
    {
        return await _context.Pacientes
            .AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .Select(p => new PacienteOpcaoAtendimentoViewModel
            {
                Id = p.Id,
                Nome = p.Nome,
                Email = p.Email ?? string.Empty,
                Telefone = p.Telefone,
                PossuiContaWeb = p.UsuarioId != null && p.UsuarioId != ""
            })
            .Take(500)
            .ToListAsync(ct);
    }

    private static string NomeCanal(CanalAtendimento canal) =>
        canal switch
        {
            CanalAtendimento.WhatsApp => "WhatsApp",
            CanalAtendimento.Sms => "SMS",
            CanalAtendimento.Email => "e-mail",
            _ => "site"
        };
}
