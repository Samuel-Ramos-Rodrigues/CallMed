using MKSANCrud.DTOs.Agente;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoConversaService
{
    private const int LimiteMensagens = 250;
    private readonly MKSANContext _context;
    private readonly AtendimentoIdentidadeService _identidade;

    public AtendimentoConversaService(
        MKSANContext context,
        AtendimentoIdentidadeService identidade)
    {
        _context = context;
        _identidade = identidade;
    }

    public async Task<ConversaAtendimento> ObterOuCriarAsync(
        CanalAtendimento canal,
        string identificador,
        int? pacienteId = null,
        string? assunto = null,
        bool reativar = true,
        CancellationToken ct = default)
    {
        var normalizado =
            AtendimentoIdentidadeService.NormalizarIdentificador(
                canal,
                identificador);

        var conversa = await _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(
                c => c.Canal == canal &&
                     c.IdentificadorExterno == normalizado,
                ct);

        if (conversa is null)
        {
            Paciente? paciente = null;

            if (pacienteId.HasValue)
            {
                paciente = await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p => p.Id == pacienteId.Value && p.Ativo,
                        ct);
            }

            paciente ??= await _identidade.ResolverPacienteAsync(
                canal,
                normalizado,
                ct);

            conversa = new ConversaAtendimento
            {
                Canal = canal,
                IdentificadorExterno = normalizado,
                PacienteId = paciente?.Id,
                Paciente = paciente,
                SessionId = CriarSessionId(
                    canal,
                    paciente?.Id,
                    normalizado),
                Assunto = Limitar(assunto, 300),
                Modo = ModoAtendimento.IA,
                Ativa = true,
                CriadoEm = DateTime.UtcNow,
                AtualizadoEm = DateTime.UtcNow,
                UltimaInteracaoEm = DateTime.UtcNow
            };

            _context.ConversasAtendimento.Add(conversa);

            try
            {
                await _context.SaveChangesAsync(ct);
                return conversa;
            }
            catch (DbUpdateException)
            {
                // Dois webhooks podem abrir a mesma conversa ao mesmo tempo.
                _context.Entry(conversa).State = EntityState.Detached;

                var existente = await _context.ConversasAtendimento
                    .Include(c => c.Paciente)
                    .FirstOrDefaultAsync(
                        c => c.Canal == canal &&
                             c.IdentificadorExterno == normalizado,
                        ct);

                if (existente is not null)
                    return existente;

                throw;
            }
        }

        var alterou = false;

        if (!conversa.PacienteId.HasValue)
        {
            Paciente? paciente = null;

            if (pacienteId.HasValue)
            {
                paciente = await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p => p.Id == pacienteId.Value && p.Ativo,
                        ct);
            }

            paciente ??= await _identidade.ResolverPacienteAsync(
                canal,
                normalizado,
                ct);

            if (paciente is not null)
            {
                conversa.PacienteId = paciente.Id;
                conversa.Paciente = paciente;
                conversa.SessionId = CriarSessionId(
                    conversa.Canal,
                    paciente.Id,
                    conversa.IdentificadorExterno);
                alterou = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(assunto))
        {
            var novoAssunto = Limitar(assunto, 300);
            if (!string.Equals(conversa.Assunto, novoAssunto, StringComparison.Ordinal))
            {
                conversa.Assunto = novoAssunto;
                alterou = true;
            }
        }

        if (reativar && !conversa.Ativa)
        {
            conversa.Ativa = true;
            alterou = true;
        }

        if (reativar)
        {
            conversa.AtualizadoEm = DateTime.UtcNow;
            alterou = true;
        }

        if (alterou)
            await _context.SaveChangesAsync(ct);

        return conversa;
    }

    public async Task<MensagemAtendimento?> RegistrarEntradaAsync(
        ConversaAtendimento conversa,
        string texto,
        string? mensagemExternaId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var externo = Limitar(mensagemExternaId, 220);

        if (!string.IsNullOrWhiteSpace(externo))
        {
            var existente = await _context.MensagensAtendimento
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    m => m.ConversaAtendimentoId == conversa.Id &&
                         m.MensagemExternaId == externo,
                    ct);

            if (existente is not null)
                return null;
        }

        var agora = DateTime.UtcNow;

        var mensagem = new MensagemAtendimento
        {
            ConversaAtendimentoId = conversa.Id,
            Direcao = DirecaoMensagemAtendimento.Entrada,
            Autor = AutorMensagemAtendimento.Paciente,
            Status = StatusMensagemAtendimento.Recebida,
            MensagemExternaId = externo,
            Texto = Limitar(texto.Trim(), 5000) ?? string.Empty,
            CriadoEm = agora
        };

        conversa.Ativa = true;
        conversa.AtualizadoEm = agora;
        conversa.UltimaInteracaoEm = agora;

        _context.MensagensAtendimento.Add(mensagem);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
            when (!string.IsNullOrWhiteSpace(externo))
        {
            // Retry concorrente do mesmo webhook.
            _context.Entry(mensagem).State = EntityState.Detached;
            return null;
        }

        await ApararAsync(conversa.Id, ct);

        return mensagem;
    }

    public async Task<MensagemAtendimento> RegistrarSaidaAsync(
        ConversaAtendimento conversa,
        string texto,
        AutorMensagemAtendimento autor,
        StatusMensagemAtendimento status,
        string? autorUsuarioId = null,
        string? mensagemExternaId = null,
        string? erro = null,
        CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;

        var mensagem = new MensagemAtendimento
        {
            ConversaAtendimentoId = conversa.Id,
            Direcao = DirecaoMensagemAtendimento.Saida,
            Autor = autor,
            Status = status,
            MensagemExternaId = Limitar(mensagemExternaId, 220),
            Texto = Limitar(texto.Trim(), 5000) ?? string.Empty,
            Erro = Limitar(erro, 1000),
            AutorUsuarioId = autorUsuarioId,
            CriadoEm = agora,
            EnviadoEm = status == StatusMensagemAtendimento.Enviada
                ? agora
                : null
        };

        conversa.AtualizadoEm = agora;
        conversa.UltimaInteracaoEm = agora;

        _context.MensagensAtendimento.Add(mensagem);
        await _context.SaveChangesAsync(ct);
        await ApararAsync(conversa.Id, ct);

        return mensagem;
    }

    public async Task<IReadOnlyList<MensagemHistoricoAgente>>
        CarregarHistoricoPacienteAsync(
            int? pacienteId,
            long conversaAtualId,
            int limite = 24,
            CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, 40);

        IQueryable<MensagemAtendimento> query =
            _context.MensagensAtendimento
                .AsNoTracking()
                .Include(m => m.Conversa);

        query = pacienteId.HasValue
            ? query.Where(m =>
                m.Conversa != null &&
                m.Conversa.PacienteId == pacienteId.Value)
            : query.Where(m =>
                m.ConversaAtendimentoId == conversaAtualId);

        var mensagens = await query
            .OrderByDescending(m => m.CriadoEm)
            .ThenByDescending(m => m.Id)
            .Take(limite)
            .OrderBy(m => m.CriadoEm)
            .ThenBy(m => m.Id)
            .Select(m => new MensagemHistoricoAgente
            {
                Papel = m.Direcao == DirecaoMensagemAtendimento.Entrada
                    ? "user"
                    : "bot",
                Texto = m.Texto
            })
            .ToListAsync(ct);

        return mensagens;
    }

    public async Task SolicitarAtendimentoHumanoAsync(
        ConversaAtendimento conversa,
        CancellationToken ct = default)
    {
        conversa.Modo = ModoAtendimento.Humano;
        conversa.ResponsavelUsuarioId = null;
        conversa.AssumidaEm = null;
        conversa.Ativa = true;
        conversa.SessionId = CriarSessionId(
            conversa.Canal,
            conversa.PacienteId,
            conversa.IdentificadorExterno);
        conversa.AtualizadoEm = DateTime.UtcNow;
        conversa.UltimaInteracaoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task AssumirAsync(
        ConversaAtendimento conversa,
        string? responsavelUsuarioId,
        CancellationToken ct = default)
    {
        conversa.Modo = ModoAtendimento.Humano;
        conversa.ResponsavelUsuarioId = responsavelUsuarioId;
        conversa.AssumidaEm = DateTime.UtcNow;
        conversa.Ativa = true;
        conversa.SessionId = CriarSessionId(
            conversa.Canal,
            conversa.PacienteId,
            conversa.IdentificadorExterno);
        conversa.AtualizadoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task DevolverParaIaAsync(
        ConversaAtendimento conversa,
        CancellationToken ct = default)
    {
        conversa.Modo = ModoAtendimento.IA;
        conversa.ResponsavelUsuarioId = null;
        conversa.AssumidaEm = null;
        conversa.Ativa = true;

        // Uma nova sessão invalida qualquer confirmação pendente anterior
        // ao atendimento humano. O histórico continua preservado no banco.
        conversa.SessionId = CriarSessionId(
            conversa.Canal,
            conversa.PacienteId,
            conversa.IdentificadorExterno);
        conversa.AtualizadoEm = DateTime.UtcNow;
        conversa.UltimaInteracaoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task EncerrarAsync(
        ConversaAtendimento conversa,
        CancellationToken ct = default)
    {
        conversa.Ativa = false;
        conversa.Modo = ModoAtendimento.IA;
        conversa.ResponsavelUsuarioId = null;
        conversa.AssumidaEm = null;
        conversa.SessionId = CriarSessionId(
            conversa.Canal,
            conversa.PacienteId,
            conversa.IdentificadorExterno);
        conversa.AtualizadoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> VincularPacienteAsync(
        ConversaAtendimento conversa,
        int pacienteId,
        CancellationToken ct = default)
    {
        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(
                p => p.Id == pacienteId && p.Ativo,
                ct);

        if (paciente is null)
            return false;

        // No canal Web o identificador é o UserId do Identity.
        // Impede vincular manualmente a conversa web de uma conta a outro paciente.
        if (conversa.Canal == CanalAtendimento.Web &&
            !string.Equals(
                paciente.UsuarioId,
                conversa.IdentificadorExterno,
                StringComparison.Ordinal))
            return false;

        conversa.PacienteId = paciente.Id;
        conversa.Paciente = paciente;
        conversa.SessionId = CriarSessionId(
            conversa.Canal,
            paciente.Id,
            conversa.IdentificadorExterno);
        conversa.AtualizadoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DesvincularPacienteAsync(
        ConversaAtendimento conversa,
        CancellationToken ct = default)
    {
        if (conversa.Canal == CanalAtendimento.Web)
            return false;

        conversa.PacienteId = null;
        conversa.Paciente = null;
        conversa.SessionId = CriarSessionId(
            conversa.Canal,
            null,
            conversa.IdentificadorExterno);
        conversa.AtualizadoEm = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<MensagemAtendimento>>
        CarregarMensagensAsync(
            long conversaId,
            int limite = 120,
            CancellationToken ct = default)
    {
        limite = Math.Clamp(limite, 1, 200);

        return await _context.MensagensAtendimento
            .AsNoTracking()
            .Where(m => m.ConversaAtendimentoId == conversaId)
            .OrderByDescending(m => m.CriadoEm)
            .ThenByDescending(m => m.Id)
            .Take(limite)
            .OrderBy(m => m.CriadoEm)
            .ThenBy(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MensagemAtendimento>>
        CarregarSaidasHumanasDepoisAsync(
            long conversaId,
            long afterId,
            CancellationToken ct = default)
    {
        return await _context.MensagensAtendimento
            .AsNoTracking()
            .Where(m =>
                m.ConversaAtendimentoId == conversaId &&
                m.Id > afterId &&
                m.Direcao == DirecaoMensagemAtendimento.Saida &&
                m.Autor == AutorMensagemAtendimento.Funcionario &&
                m.Status == StatusMensagemAtendimento.Enviada)
            .OrderBy(m => m.Id)
            .Take(30)
            .ToListAsync(ct);
    }

    private async Task ApararAsync(long conversaId, CancellationToken ct)
    {
        var ids = await _context.MensagensAtendimento
            .Where(m => m.ConversaAtendimentoId == conversaId)
            .OrderByDescending(m => m.CriadoEm)
            .ThenByDescending(m => m.Id)
            .Skip(LimiteMensagens)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (ids.Count == 0)
            return;

        await _context.MensagensAtendimento
            .Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(ct);
    }

    private static string CriarSessionId(
        CanalAtendimento canal,
        int? pacienteId,
        string identificador)
    {
        var origem = pacienteId.HasValue
            ? $"paciente-{pacienteId.Value}"
            : identificador;

        var seguro = new string(
            origem
                .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                .Take(100)
                .ToArray());

        var sufixo = Guid.NewGuid().ToString("N")[..12];
        return $"{canal.ToString().ToLowerInvariant()}-{seguro}-{sufixo}";
    }

    private static string? Limitar(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        var texto = valor.Trim();

        return texto.Length <= limite
            ? texto
            : texto[..limite];
    }
}
