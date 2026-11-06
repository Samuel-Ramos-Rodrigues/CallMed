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
    private IQueryable<MensagemAtendimento> Visiveis(long conversaId) => db.MensagensAtendimento.AsNoTracking().Where(m =>
        m.ConversaAtendimentoId == conversaId && (m.ChaveEnvio == null || !m.ChaveEnvio.StartsWith("feedback:")) &&
        m.Status != StatusMensagemAtendimento.AguardandoAprovacao && m.Status != StatusMensagemAtendimento.Ignorada);
    private IQueryable<MensagemAtendimento> Entradas(long conversaId) => Visiveis(conversaId).Where(m =>
        m.Direcao == DirecaoMensagemAtendimento.Entrada && m.Autor == AutorMensagemAtendimento.Paciente);
    private async Task<ConversaAtendimento> Propria(CancellationToken ct)
    {
        var conversa = await conversas.ObterOuCriarAsync(CanalAtendimento.Web, User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            (int)HttpContext.Items[MobileAuth.PacienteItem]!, reativar: false, ct: ct);
        // Fechamentos legados sem pesquisa também precisam de uma fronteira
        // persistida. Não solicita uma avaliação retroativa ao paciente.
        if (!conversa.Ativa && !await db.AvaliacoesAtendimento.AnyAsync(a => a.ConversaAtendimentoId == conversa.Id, ct))
        {
            var a = await feedback.EncerrarAsync(conversa, ct);
            if (a is not null) { a.DispensadaEm = DateTime.UtcNow; a.Versao = Guid.NewGuid(); await db.SaveChangesAsync(ct); }
        }
        return conversa;
    }
    private static object[] Mensagens(IEnumerable<MensagemAtendimento> mensagens) => mensagens.OrderBy(m => m.Id).Select(m => (object)new {
        m.Id, m.Texto, autor = m.Direcao == DirecaoMensagemAtendimento.Entrada ? "paciente" : m.Autor == AutorMensagemAtendimento.Funcionario ? "equipe" : "assistente", criadoEm = m.CriadoEm }).ToArray();

    [HttpGet("historico")]
    public async Task<IActionResult> Historico(long? antesDeMensagemId = null, CancellationToken ct = default)
    {
        var conversa = await Propria(ct);
        for (var tentativa = 0; tentativa < 2; tentativa++)
        {
            var sessaoLida = conversa.SessionId;
            var ultima = await db.AvaliacoesAtendimento.AsNoTracking().Where(a => a.ConversaAtendimentoId == conversa.Id)
                .OrderByDescending(a => a.UltimaEntradaId).FirstOrDefaultAsync(ct);
            var limite = ultima?.UltimaEntradaId ?? 0;
            var inicio = await Entradas(conversa.Id).Where(m => m.Id > limite).MinAsync(m => (long?)m.Id, ct);
            var query = Visiveis(conversa.Id).Where(m => inicio.HasValue && m.Id >= inicio.Value);
            if (antesDeMensagemId.HasValue) query = query.Where(m => m.Id < antesDeMensagemId.Value);
            var lista = await query.OrderByDescending(m => m.Id).Take(61).ToListAsync(ct);
            await conversas.RecarregarAsync(conversa, ct);
            if (sessaoLida != conversa.SessionId && tentativa == 0) continue;
            // Uma entrada posterior ao último encerramento pertence ao atendimento
            // atual, mesmo se um cliente antigo deixou a flag Ativa desatualizada.
            var encerrada = !conversa.Ativa && !inicio.HasValue;
            var a = encerrada ? await feedback.ObterPendenteAsync(conversa, ct) : null;
            var pagina = lista.Take(60).ToArray();
            return Ok(new { historicoVersao = 2, conversaId = conversa.Id, sessionId = conversa.SessionId,
                modo = conversa.Modo.ToString(), encerrada, cicloId = ultima?.Id.ToString() ?? "inicial",
                haAnteriores = ultima is not null, haMaisMensagens = lista.Count > 60,
                avaliacao = a is null ? null : new { id = a.Id, conversaId = conversa.Id },
                mensagens = encerrada ? [] : Mensagens(pagina) });
        }
        return Conflict(new { mensagem = "O atendimento foi atualizado. Tente abrir a conversa novamente." });
    }
    [HttpGet("anteriores")]
    public async Task<IActionResult> Anteriores(Guid? antesDe = null, CancellationToken ct = default)
    {
        var conversa = await Propria(ct);
        var query = db.AvaliacoesAtendimento.AsNoTracking().Where(a => a.ConversaAtendimentoId == conversa.Id);
        if (antesDe.HasValue)
        {
            var cursor = await query.SingleOrDefaultAsync(a => a.Id == antesDe.Value, ct);
            if (cursor is null) return NotFound();
            query = query.Where(a => a.UltimaEntradaId < cursor.UltimaEntradaId);
        }
        var lista = await query.OrderByDescending(a => a.UltimaEntradaId).Take(21).ToListAsync(ct);
        var pagina = lista.Take(20).ToArray(); var ids = pagina.Select(a => a.UltimaEntradaId).ToArray();
        var entradas = await Entradas(conversa.Id).Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Texto, ct);
        return Ok(new { itens = pagina.Select(a => new { a.Id, finalizadoEm = a.CriadoEm,
            assunto = entradas.TryGetValue(a.UltimaEntradaId, out var texto) ? texto[..Math.Min(100, texto.Length)] : "Atendimento anterior" }),
            proximoCursor = lista.Count > 20 ? pagina.Last().Id.ToString() : null });
    }
    [HttpGet("anteriores/{id:guid}")]
    public async Task<IActionResult> Anterior(Guid id, long? antesDeMensagemId = null, CancellationToken ct = default)
    {
        var conversa = await Propria(ct);
        var alvo = await db.AvaliacoesAtendimento.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.ConversaAtendimentoId == conversa.Id, ct);
        if (alvo is null) return NotFound();
        var anterior = await db.AvaliacoesAtendimento.Where(a => a.ConversaAtendimentoId == conversa.Id && a.UltimaEntradaId < alvo.UltimaEntradaId)
            .OrderByDescending(a => a.UltimaEntradaId).Select(a => (long?)a.UltimaEntradaId).FirstOrDefaultAsync(ct) ?? 0;
        var inicio = await Entradas(conversa.Id).Where(m => m.Id > anterior && m.Id <= alvo.UltimaEntradaId).MinAsync(m => (long?)m.Id, ct);
        var fim = await Entradas(conversa.Id).Where(m => m.Id > alvo.UltimaEntradaId).MinAsync(m => (long?)m.Id, ct);
        var query = Visiveis(conversa.Id).Where(m => inicio.HasValue && m.Id >= inicio.Value && (!fim.HasValue || m.Id < fim.Value));
        if (antesDeMensagemId.HasValue) query = query.Where(m => m.Id < antesDeMensagemId.Value);
        var lista = await query.OrderByDescending(m => m.Id).Take(61).ToListAsync(ct);
        return Ok(new { alvo.Id, finalizadoEm = alvo.CriadoEm, haMaisMensagens = lista.Count > 60, mensagens = Mensagens(lista.Take(60)) });
    }
    [HttpPost("iniciar"), EnableRateLimiting("agente")]
    public async Task<IActionResult> Iniciar(NovoAtendimentoInput input, CancellationToken ct)
    {
        var conversa = await Propria(ct);
        var ultima = await db.AvaliacoesAtendimento.Where(a => a.ConversaAtendimentoId == conversa.Id)
            .OrderByDescending(a => a.UltimaEntradaId).Select(a => (long?)a.UltimaEntradaId).FirstOrDefaultAsync(ct) ?? 0;
        var temAtual = await Entradas(conversa.Id).AnyAsync(m => m.Id > ultima, ct);
        if (conversa.Ativa && temAtual && !input.ConfirmarEncerramento)
            return BadRequest(new { mensagem = "Confirme que deseja finalizar o atendimento atual e iniciar outro." });
        if (temAtual) await feedback.EncerrarAsync(conversa, ct);
        await conversas.DevolverParaIaAsync(conversa, ct);
        return await Historico(ct: ct);
    }
    public sealed class NovoAtendimentoInput { public bool ConfirmarEncerramento { get; set; } }
    [HttpPost("mensagens"), EnableRateLimiting("agente")]
    public Task<IActionResult> Enviar(MensagemAgenteRequest request, CancellationToken ct) => Agente().Enviar(request, ct);
    [HttpPost("encerrar"), EnableRateLimiting("agente")]
    public Task<IActionResult> Encerrar(CancellationToken ct) => Agente().Encerrar(ct);
    [HttpPost("avaliar"), EnableRateLimiting("agente")]
    public Task<IActionResult> Avaliar(AgenteController.AvaliacaoRequest request, CancellationToken ct) => Agente().Avaliar(request, ct);
}
