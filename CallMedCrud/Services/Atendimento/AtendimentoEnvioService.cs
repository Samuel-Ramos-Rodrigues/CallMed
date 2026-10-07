using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.DTOs.Atendimento;
using MKSANCrud.Models.Atendimento;
using Npgsql;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoEnvioService(
    IEnumerable<ICanalAtendimentoSender> senders,
    MKSANContext context,
    TimeProvider clock,
    ILogger<AtendimentoEnvioService> logger)
{
    private readonly IReadOnlyDictionary<CanalAtendimento, ICanalAtendimentoSender> _senders =
        senders.ToDictionary(s => s.Canal);

    public bool CanalConfigurado(CanalAtendimento canal) => canal == CanalAtendimento.Web ||
        (_senders.TryGetValue(canal, out var sender) && sender.Configurado);

    public async Task<MensagemAtendimento> EnviarAsync(
        ConversaAtendimento conversa, string texto, AutorMensagemAtendimento autor,
        string? autorUsuarioId = null, string? assunto = null, CancellationToken ct = default)
    {
        var mensagem = NovaMensagem(conversa, texto, autor, autorUsuarioId);
        context.MensagensAtendimento.Add(mensagem);
        await context.SaveChangesAsync(ct);
        return await EnviarReservadaAsync(mensagem, conversa, assunto, ct);
    }

    // Uma chave representa um evento de negócio, não uma tentativa de transporte.
    public async Task<MensagemAtendimento> EnviarAvisoAsync(
        ConversaAtendimento conversa, string texto, string chave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > 160)
            throw new ArgumentException("Chave de aviso inválida.", nameof(chave));
        var existente = await context.MensagensAtendimento.Include(m => m.Conversa).FirstOrDefaultAsync(m => m.ChaveEnvio == chave, ct);
        if (existente is not null)
            return await TentarExistenteAsync(existente, existente.Conversa ?? conversa, true, ct);

        // Aproveita um aviso equivalente da versão anterior sem apagar o histórico.
        var conteudo = NormalizarTexto(texto);
        var anterior = await context.MensagensAtendimento
            .Where(m => m.ConversaAtendimentoId == conversa.Id && m.ChaveEnvio == null &&
                m.Direcao == DirecaoMensagemAtendimento.Saida && m.Autor == AutorMensagemAtendimento.Sistema &&
                m.Texto == conteudo)
            .OrderByDescending(m => m.Status == StatusMensagemAtendimento.Enviada)
            .ThenBy(m => m.Id).FirstOrDefaultAsync(ct);
        var mensagem = anterior ?? NovaMensagem(conversa, conteudo, AutorMensagemAtendimento.Sistema, null);
        mensagem.ChaveEnvio = chave;
        mensagem.VersaoEnvio = Guid.NewGuid();
        if (anterior is null) context.MensagensAtendimento.Add(mensagem);
        else if (mensagem.Status == StatusMensagemAtendimento.Falhou &&
                 mensagem.Erro?.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase) == true)
            mensagem.ProximaTentativaEm = clock.GetUtcNow().UtcDateTime;
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException ||
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_MensagensAtendimento_ChaveEnvio" })
        {
            context.Entry(mensagem).State = EntityState.Detached;
            var reservada = await context.MensagensAtendimento.Include(m => m.Conversa).FirstOrDefaultAsync(m => m.ChaveEnvio == chave, ct);
            if (reservada is null) throw;
            return await TentarExistenteAsync(reservada, reservada.Conversa ?? conversa, true, ct);
        }
        return anterior is null
            ? await EnviarReservadaAsync(mensagem, conversa, null, ct)
            : await TentarExistenteAsync(mensagem, conversa, true, ct);
    }

    public async Task<MensagemAtendimento?> ReenviarAsync(long mensagemId, CancellationToken ct = default)
    {
        var mensagem = await context.MensagensAtendimento.Include(m => m.Conversa)
            .FirstOrDefaultAsync(m => m.Id == mensagemId && m.Direcao == DirecaoMensagemAtendimento.Saida, ct);
        return mensagem?.Conversa is null ? null
            : await TentarExistenteAsync(mensagem, mensagem.Conversa, false, ct);
    }

    private async Task<MensagemAtendimento> TentarExistenteAsync(
        MensagemAtendimento mensagem, ConversaAtendimento conversa, bool automatico, CancellationToken ct)
    {
        await context.Entry(mensagem).ReloadAsync(ct);
        if (mensagem.Status != StatusMensagemAtendimento.Falhou || mensagem.ReenvioBloqueado ||
            mensagem.ProximaTentativaEm > clock.GetUtcNow().UtcDateTime ||
            (automatico && mensagem.ProximaTentativaEm is null))
            return mensagem;

        mensagem.Status = StatusMensagemAtendimento.Processando;
        mensagem.VersaoEnvio = Guid.NewGuid();
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            await context.Entry(mensagem).ReloadAsync(ct);
            return mensagem;
        }
        return await EnviarReservadaAsync(mensagem, conversa, null, ct);
    }

    private async Task<MensagemAtendimento> EnviarReservadaAsync(
        MensagemAtendimento mensagem, ConversaAtendimento conversa, string? assunto, CancellationToken ct)
    {
        CanalEnvioResultado resultado;
        try
        {
            if (conversa.Canal == CanalAtendimento.Web) resultado = CanalEnvioResultado.Ok();
            else if (!_senders.TryGetValue(conversa.Canal, out var sender))
                resultado = CanalEnvioResultado.Falha("Canal sem provedor registrado.");
            else if (!sender.Configurado)
                resultado = CanalEnvioResultado.Falha("Canal ainda não foi configurado.");
            else resultado = await sender.EnviarAsync(conversa.IdentificadorExterno, mensagem.Texto,
                assunto ?? conversa.Assunto, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resultado de envio não confirmado na conversa {ConversaId}.", conversa.Id);
            resultado = new CanalEnvioResultado
            {
                Erro = "O provedor não confirmou o envio. Confira o canal antes de enviar outra mensagem.",
                ReenvioBloqueado = true
            };
        }
        mensagem.Status = resultado.Sucesso ? StatusMensagemAtendimento.Enviada : StatusMensagemAtendimento.Falhou;
        mensagem.MensagemExternaId = resultado.MensagemExternaId;
        mensagem.Erro = resultado.Erro;
        mensagem.ProximaTentativaEm = resultado.ProximaTentativaEm;
        mensagem.ReenvioBloqueado = resultado.ReenvioBloqueado;
        mensagem.EnviadoEm = resultado.Sucesso ? clock.GetUtcNow().UtcDateTime : null;
        mensagem.VersaoEnvio = Guid.NewGuid();
        conversa.AtualizadoEm = clock.GetUtcNow().UtcDateTime;
        conversa.UltimaInteracaoEm = conversa.AtualizadoEm;
        // Guarda o resultado mesmo se o navegador fechar/cancelar a requisição.
        using var gravacao = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await context.SaveChangesAsync(gravacao.Token);
        return mensagem;
    }

    private MensagemAtendimento NovaMensagem(ConversaAtendimento conversa, string texto,
        AutorMensagemAtendimento autor, string? usuarioId) => new()
    {
        ConversaAtendimentoId = conversa.Id,
        Direcao = DirecaoMensagemAtendimento.Saida,
        Autor = autor,
        AutorUsuarioId = usuarioId,
        Texto = NormalizarTexto(texto),
        Status = StatusMensagemAtendimento.Processando,
        CriadoEm = clock.GetUtcNow().UtcDateTime
    };

    private static string NormalizarTexto(string texto)
    {
        var valor = texto.Trim();
        if (valor.Length is 0 or > 5000)
            throw new ArgumentException("A mensagem deve ter entre 1 e 5000 caracteres.", nameof(texto));
        return valor;
    }
}
