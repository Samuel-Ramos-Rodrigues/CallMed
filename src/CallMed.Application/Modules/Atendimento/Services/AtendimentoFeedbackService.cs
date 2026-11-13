using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using Npgsql;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoFeedbackService(
    MKSANContext context, AtendimentoConversaService conversas,
    AtendimentoEnvioService envio, TimeProvider clock)
{
    public const string Convite = "Foi um prazer ajudar! Como você avalia seu atendimento na CallMed, de 1 (muito ruim) a 5 (muito bom)? A avaliação é opcional. Se quiser, inclua um comentário: NOTA 5 - fui bem atendido. Quando precisar, estarei por aqui. Para continuar, envie sua pergunta normalmente.";
    public const string Obrigado = "Obrigado pela avaliação! Seu feedback ajuda a melhorar o atendimento da CallMed.";
    public static bool CanalDigital(CanalAtendimento canal) => canal is CanalAtendimento.Web or CanalAtendimento.WhatsApp or CanalAtendimento.Sms or CanalAtendimento.Email;
    private DateTime Agora => clock.GetUtcNow().UtcDateTime;

    public static bool EhEncerramento(string texto)
    {
        var s = SemAcentos(texto).Trim().TrimEnd('.', '!', '?', ' ', '\n');
        // Frases inteiras: não interpreta cancelamento de consulta nem agradecimento intermediário.
        return Regex.IsMatch(s, @"^(?:(?:obrigad[oa]|valeu)[,!. ]+)?(?:pode encerrar(?: o atendimento| a conversa)?|encerrar atendimento|encerrar conversa|era so isso(?:[, ]+obrigad[oa])?|por hoje e so|tchau|ate mais|finalizar atendimento)$", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    private static string SemAcentos(string s) => string.Concat(s.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Normalize(NormalizationForm.FormC);

    public Task<ConversaAtendimento?> ObterWebAsync(string usuarioId, CancellationToken ct) =>
        context.ConversasAtendimento.FirstOrDefaultAsync(c => c.Canal == CanalAtendimento.Web && c.IdentificadorExterno == usuarioId, ct);

    public async Task<AvaliacaoAtendimento?> EncerrarPorMensagemAsync(ConversaAtendimento conversa, string texto, CancellationToken ct = default)
    {
        await using var transaction = context.Database.IsRelational() && context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(ct) : null;
        if (conversa.Ativa) await conversas.RegistrarEntradaAsync(conversa, texto, ct: ct);
        var a = await EncerrarAsync(conversa, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return a;
    }

    public async Task<AvaliacaoAtendimento?> EncerrarAsync(ConversaAtendimento conversa, CancellationToken ct = default)
    {
        // Encerramento e criação da pesquisa são confirmados juntos no PostgreSQL.
        await using var transaction = context.Database.IsRelational() && context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(ct) : null;
        var avaliacao = await EncerrarCoreAsync(conversa, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return avaliacao;
    }

    private async Task<AvaliacaoAtendimento?> EncerrarCoreAsync(ConversaAtendimento conversa, CancellationToken ct)
    {
        if (!CanalDigital(conversa.Canal)) { await conversas.EncerrarAsync(conversa, ct); return null; }
        var ultima = await context.MensagensAtendimento.Where(m => m.ConversaAtendimentoId == conversa.Id &&
            m.Direcao == DirecaoMensagemAtendimento.Entrada && m.Autor == AutorMensagemAtendimento.Paciente)
            .OrderByDescending(m => m.Id).Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (conversa.Ativa) await conversas.EncerrarAsync(conversa, ct);
        if (ultima is null) return null; // Uma conversa vazia não gera pesquisa.
        var existente = await context.AvaliacoesAtendimento.FirstOrDefaultAsync(a => a.ConversaAtendimentoId == conversa.Id && a.UltimaEntradaId == ultima, ct);
        if (existente is not null) return existente;
        // Só a pesquisa do encerramento mais recente continua pendente.
        var antigas = await context.AvaliacoesAtendimento.Where(a => a.ConversaAtendimentoId == conversa.Id &&
            a.UltimaEntradaId != ultima && a.Nota == null && a.DispensadaEm == null).ToListAsync(ct);
        foreach (var antiga in antigas) { antiga.DispensadaEm = Agora; antiga.Versao = Guid.NewGuid(); }
        var avaliacao = new AvaliacaoAtendimento { ConversaAtendimentoId = conversa.Id, UltimaEntradaId = ultima.Value, Canal = conversa.Canal, CriadoEm = Agora };
        context.AvaliacoesAtendimento.Add(avaliacao);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(avaliacao).State = EntityState.Detached;
            return await context.AvaliacoesAtendimento.SingleAsync(a => a.ConversaAtendimentoId == conversa.Id && a.UltimaEntradaId == ultima, ct);
        }
        return avaliacao;
    }

    public async Task<AvaliacaoAtendimento?> ObterPendenteAsync(ConversaAtendimento conversa, CancellationToken ct = default)
    {
        if (conversa.Ativa) return null;
        var limite = Agora.AddDays(-7);
        var ultima = await context.AvaliacoesAtendimento.Where(a => a.ConversaAtendimentoId == conversa.Id)
            .OrderByDescending(a => a.CriadoEm).FirstOrDefaultAsync(ct);
        return ultima is { Nota: null, DispensadaEm: null } && ultima.CriadoEm >= limite ? ultima : null;
    }

    public async Task<ResultadoAvaliacao> RegistrarAsync(Guid id, long conversaId, int? nota, string? comentario, bool dispensar,
        string? externaId = null, CancellationToken ct = default)
    {
        if (!dispensar && (nota is null or < 1 or > 5 || (comentario?.Length ?? 0) > 1000)) return ResultadoAvaliacao.Invalida;
        var a = await context.AvaliacoesAtendimento.Include(a => a.Conversa).FirstOrDefaultAsync(a => a.Id == id && a.ConversaAtendimentoId == conversaId, ct);
        if (a is null) return ResultadoAvaliacao.Inexistente;
        if (a.Nota.HasValue || a.DispensadaEm.HasValue) return ResultadoAvaliacao.JaRegistrada;
        if (a.Conversa?.Ativa != false || a.CriadoEm < Agora.AddDays(-7)) return ResultadoAvaliacao.Expirada;
        if (dispensar) a.DispensadaEm = Agora;
        else { a.Nota = nota; a.Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(); a.RespondidoEm = Agora; }
        a.RespostaExternaId = externaId is { Length: > 220 } ? externaId[..220] : externaId;
        a.Versao = Guid.NewGuid();
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { await context.Entry(a).ReloadAsync(ct); return ResultadoAvaliacao.JaRegistrada; }
        return ResultadoAvaliacao.Registrada;
    }

    // Apenas banco: o webhook de SMS precisa confirmar o recebimento rapidamente.
    public async Task<bool> TratarEntradaAsync(CanalMensagemEntrada entrada, CancellationToken ct = default)
    {
        if (!CanalDigital(entrada.Canal)) return false;
        var bruto = entrada.Texto.Length > 5000 ? entrada.Texto[..5000] : entrada.Texto;
        var texto = (entrada.Canal == CanalAtendimento.Email ? AtendimentoEmailTexto.Normalizar(bruto) : bruto).Trim();
        var encerramento = EhEncerramento(texto);
        var match = Regex.Match(texto, @"^(?:nota\s*[:=]?\s*)?([0-9]{1,2})(?:(?:\s*[-–—:,]\s*|\s+)(.*))?$", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
        var pular = SemAcentos(texto) is "pular avaliacao" or "nao avaliar";
        if (entrada.Canal == CanalAtendimento.Web && string.IsNullOrWhiteSpace(entrada.MensagemExternaId) &&
            !encerramento && !match.Success && !pular) return false;
        var identificador = AtendimentoIdentidadeService.NormalizarIdentificador(entrada.Canal, entrada.Identificador);
        var conversa = await context.ConversasAtendimento.FirstOrDefaultAsync(c => c.Canal == entrada.Canal && c.IdentificadorExterno == identificador &&
            (!entrada.ConversaAtendimentoId.HasValue || c.Id == entrada.ConversaAtendimentoId.Value), ct);
        if (conversa is null) return false;
        // Uma reentrega antiga de feedback não pode entrar como nova pergunta após a reabertura.
        if (!string.IsNullOrWhiteSpace(entrada.MensagemExternaId) && await context.AvaliacoesAtendimento.AnyAsync(a =>
            a.ConversaAtendimentoId == conversa.Id && a.RespostaExternaId == entrada.MensagemExternaId, ct)) return true;
        if (!string.IsNullOrWhiteSpace(entrada.MensagemExternaId) && await context.MensagensAtendimento.AnyAsync(m =>
            m.ConversaAtendimentoId == conversa.Id && m.Direcao == DirecaoMensagemAtendimento.Entrada && m.MensagemExternaId == entrada.MensagemExternaId, ct)) return true;
        if (encerramento)
        {
            await using var transaction = context.Database.IsRelational() && context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(ct) : null;
            if (conversa.Ativa)
                await conversas.RegistrarEntradaAsync(conversa, texto, entrada.MensagemExternaId, ct);
            else if (!string.IsNullOrWhiteSpace(entrada.MensagemExternaId))
            {
                // Deduplica também despedidas repetidas após o encerramento, sem reativar a conversa.
                context.MensagensAtendimento.Add(new MensagemAtendimento { ConversaAtendimentoId = conversa.Id,
                    Texto = texto, MensagemExternaId = entrada.MensagemExternaId,
                    Autor = AutorMensagemAtendimento.Sistema, Direcao = DirecaoMensagemAtendimento.Entrada,
                    Status = StatusMensagemAtendimento.Ignorada, CriadoEm = Agora });
                await context.SaveChangesAsync(ct);
            }
            await EncerrarAsync(conversa, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return true;
        }
        if (conversa.Ativa) return false; // "1" pode ser uma opção de agendamento.
        var limite = Agora.AddDays(-7);
        var a = await context.AvaliacoesAtendimento.Where(a => a.ConversaAtendimentoId == conversa.Id && a.CriadoEm >= limite)
            .OrderByDescending(a => a.CriadoEm).FirstOrDefaultAsync(ct);
        if (a is null) return false;
        if (!match.Success && !pular) return false;
        if (a.Nota.HasValue || a.DispensadaEm.HasValue) return true;
        int? nota = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        var resultado = await RegistrarAsync(a.Id, conversa.Id, nota, match.Success ? match.Groups[2].Value : null, pular, entrada.MensagemExternaId, ct);
        if (resultado == ResultadoAvaliacao.Invalida && a.OrientacaoProcessadaEm is null)
        {
            a.OrientacaoPendente = true; a.Versao = Guid.NewGuid(); await context.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task ProcessarEnviosAsync(CancellationToken ct = default)
    {
        var limite = Agora.AddDays(-7);
        var lista = await context.AvaliacoesAtendimento.Include(a => a.Conversa).Where(a => a.Canal != CanalAtendimento.Web && a.CriadoEm >= limite &&
            ((a.ConviteProcessadoEm == null && a.Nota == null && a.DispensadaEm == null) ||
             (a.Nota != null && a.AgradecimentoProcessadoEm == null) || (a.OrientacaoPendente && a.OrientacaoProcessadaEm == null)))
            .OrderBy(a => a.CriadoEm).Take(30).ToListAsync(ct);
        foreach (var a in lista)
        {
            if (a.Conversa is not { } conversa) continue;
            if (conversa.Ativa) { a.ConviteProcessadoEm ??= Agora; a.AgradecimentoProcessadoEm ??= Agora; a.OrientacaoProcessadaEm ??= Agora; }
            else if (a.Nota.HasValue)
            {
                await envio.EnviarAvisoAsync(conversa, Obrigado, $"feedback:obrigado:{a.Id}", ct);
                a.AgradecimentoProcessadoEm = Agora;
            }
            else if (a.DispensadaEm is null)
            {
                if (a.ConviteProcessadoEm is null)
                {
                    await envio.EnviarAvisoAsync(conversa, conversa.Canal == CanalAtendimento.Sms
                        ? "Foi um prazer ajudar! Avalie a CallMed de 1 (ruim) a 5 (otimo). Opcional: NOTA 5 - comentario. Quando precisar, estarei por aqui."
                        : Convite, $"feedback:convite:{a.Id}", ct);
                    a.ConviteProcessadoEm = Agora;
                }
                if (a.OrientacaoPendente && a.OrientacaoProcessadaEm is null)
                {
                    await envio.EnviarAvisoAsync(conversa, "Para avaliar, responda de 1 a 5. Exemplo: NOTA 5 - bom atendimento. Se preferir, responda PULAR AVALIAÇÃO.", $"feedback:orientacao:{a.Id}", ct);
                    a.OrientacaoProcessadaEm = Agora;
                }
            }
            a.OrientacaoPendente = false; a.Versao = Guid.NewGuid();
            try { await context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { await context.Entry(a).ReloadAsync(ct); }
        }
    }
}

public enum ResultadoAvaliacao { Registrada, JaRegistrada, Invalida, Inexistente, Expirada }

public sealed class AtendimentoFeedbackBackgroundService(IServiceScopeFactory scopes, ILogger<AtendimentoFeedbackBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AtendimentoFeedbackService>().ProcessarEnviosAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Falha no processamento das avaliações de atendimento."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
