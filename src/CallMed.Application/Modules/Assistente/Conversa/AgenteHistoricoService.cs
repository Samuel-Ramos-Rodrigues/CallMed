using MKSANCrud.DTOs.Agente;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Agente;

/// <summary>
/// Persiste somente o histórico visível do chat. Function calls e confirmações
/// pendentes continuam exclusivamente no estado confiável do servidor e nunca
/// são reconstruídas a partir do banco/localStorage.
/// </summary>
public sealed class AgenteHistoricoService
{
    private const int LimiteMensagensPorConversa = 100;
    private readonly MKSANContext _context;
    private readonly ILogger<AgenteHistoricoService> _logger;

    public AgenteHistoricoService(
        MKSANContext context,
        ILogger<AgenteHistoricoService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MensagemHistoricoAgente>> CarregarAsync(
        string usuarioId,
        string? sessionId,
        int limite = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId) || string.IsNullOrWhiteSpace(sessionId))
            return [];

        limite = Math.Clamp(limite, 1, 40);

        var conversa = await _context.ConversasAgente
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId && c.SessionId == sessionId)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);

        if (conversa is null)
            return [];

        var mensagens = await _context.MensagensAgente
            .AsNoTracking()
            .Where(m => m.ConversaAgenteId == conversa.Id)
            .OrderByDescending(m => m.CriadoEm)
            .ThenByDescending(m => m.Id)
            .Take(limite)
            .OrderBy(m => m.CriadoEm)
            .ThenBy(m => m.Id)
            .Select(m => new MensagemHistoricoAgente
            {
                Papel = m.Papel,
                Texto = m.Texto
            })
            .ToListAsync(ct);

        return mensagens;
    }

    public async Task SalvarInteracaoAsync(
        string usuarioId,
        string sessionId,
        string mensagemUsuario,
        string respostaAssistente,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId) || string.IsNullOrWhiteSpace(sessionId))
            return;

        var agora = DateTime.UtcNow;
        var conversa = await _context.ConversasAgente
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.SessionId == sessionId, ct);

        if (conversa is null)
        {
            conversa = new ConversaAgente
            {
                UsuarioId = usuarioId,
                SessionId = Limitar(sessionId, 120),
                CriadoEm = agora,
                AtualizadoEm = agora
            };

            _context.ConversasAgente.Add(conversa);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Outra requisição da mesma sessão pode ter criado a conversa.
                _context.Entry(conversa).State = EntityState.Detached;
                conversa = await _context.ConversasAgente
                    .FirstAsync(c => c.UsuarioId == usuarioId && c.SessionId == sessionId, ct);
            }
        }

        conversa.AtualizadoEm = agora;

        if (!string.IsNullOrWhiteSpace(mensagemUsuario))
        {
            _context.MensagensAgente.Add(new MensagemConversaAgente
            {
                ConversaAgenteId = conversa.Id,
                Papel = "user",
                Texto = Limitar(mensagemUsuario.Trim(), 2000),
                CriadoEm = agora
            });
        }

        if (!string.IsNullOrWhiteSpace(respostaAssistente))
        {
            _context.MensagensAgente.Add(new MensagemConversaAgente
            {
                ConversaAgenteId = conversa.Id,
                Papel = "bot",
                Texto = Limitar(respostaAssistente.Trim(), 2000),
                CriadoEm = agora.AddTicks(1)
            });
        }

        await _context.SaveChangesAsync(ct);
        await ApararAsync(conversa.Id, ct);
    }

    private async Task ApararAsync(int conversaId, CancellationToken ct)
    {
        var idsRemover = await _context.MensagensAgente
            .Where(m => m.ConversaAgenteId == conversaId)
            .OrderByDescending(m => m.CriadoEm)
            .ThenByDescending(m => m.Id)
            .Skip(LimiteMensagensPorConversa)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (idsRemover.Count == 0)
            return;

        await _context.MensagensAgente
            .Where(m => idsRemover.Contains(m.Id))
            .ExecuteDeleteAsync(ct);

        _logger.LogDebug(
            "Histórico do agente aparado: {Quantidade} mensagens antigas removidas da conversa {ConversaId}.",
            idsRemover.Count,
            conversaId);
    }

    private static string Limitar(string valor, int limite) =>
        valor.Length <= limite ? valor : valor[..limite];
}
