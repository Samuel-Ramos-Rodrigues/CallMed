using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Agente;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;
using MKSANCrud.Services.Mobile;
using System.Security.Claims;

namespace MKSANCrud.Controllers.Mobile;

[ApiController, Route("api/mobile/v1/atendimento"), Authorize(Policy = MobileAuth.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(24576)]
public sealed class MobileAtendimentoController(MKSANContext db, AtendimentoConversaService conversas, AtendimentoFeedbackService feedback) : ControllerBase
{
    // Reaproveita o fluxo web já validado: histórico, posse da conversa, pausa humana,
    // confirmação de mutações, timeout e feedback. A API usa Bearer + policy, não cookies/CSRF.
    private AgenteController Agente()
    {
        var controller = ActivatorUtilities.CreateInstance<AgenteController>(HttpContext.RequestServices);
        controller.ControllerContext = new ControllerContext { HttpContext = HttpContext };
        return controller;
    }
    [HttpGet("historico")]
    public async Task<IActionResult> Historico(CancellationToken ct)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var conversa = await conversas.ObterOuCriarAsync(CanalAtendimento.Web, uid, (int)HttpContext.Items[MobileAuth.PacienteItem]!, reativar: false, ct: ct);
        var mensagens = await db.MensagensAtendimento.AsNoTracking().Where(m => m.ConversaAtendimentoId == conversa.Id &&
            (m.ChaveEnvio == null || !m.ChaveEnvio.StartsWith("feedback:")) && m.Status != StatusMensagemAtendimento.AguardandoAprovacao)
            .OrderByDescending(m => m.Id).Take(60).ToListAsync(ct);
        var a = await feedback.ObterPendenteAsync(conversa, ct);
        return Ok(new { conversaId = conversa.Id, sessionId = conversa.SessionId, modo = conversa.Modo.ToString(), encerrada = !conversa.Ativa,
            avaliacao = a is null ? null : new { id = a.Id, conversaId = conversa.Id },
            mensagens = mensagens.OrderBy(m => m.Id).Select(m => new { m.Id, m.Texto, autor = m.Direcao == DirecaoMensagemAtendimento.Entrada ? "paciente" : m.Autor == AutorMensagemAtendimento.Funcionario ? "equipe" : "assistente", criadoEm = m.CriadoEm }) });
    }
    [HttpPost("mensagens"), EnableRateLimiting("agente")]
    public Task<IActionResult> Enviar(MensagemAgenteRequest request, CancellationToken ct) => Agente().Enviar(request, ct);
    [HttpPost("encerrar"), EnableRateLimiting("agente")]
    public Task<IActionResult> Encerrar(CancellationToken ct) => Agente().Encerrar(ct);
    [HttpPost("avaliar"), EnableRateLimiting("agente")]
    public Task<IActionResult> Avaliar(AgenteController.AvaliacaoRequest request, CancellationToken ct) => Agente().Avaliar(request, ct);
}
