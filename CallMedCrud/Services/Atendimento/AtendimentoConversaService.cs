using MKSANCrud.DTOs.Agente;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Options;
using Microsoft.Extensions.Options;

namespace MKSANCrud.Services.Atendimento;

public sealed class AtendimentoConversaService
{
    private const int LimiteMensagens = 250;
    private readonly MKSANContext _context;
    private readonly AtendimentoIdentidadeService _identidade;
    private readonly bool _emailAutomatico;

    public AtendimentoConversaService(
        MKSANContext context,
        AtendimentoIdentidadeService identidade,
        IOptions<AtendimentoEmailOptions>? emailOptions = null)
    {
        _context = context;
        _identidade = identidade;
        _emailAutomatico = emailOptions?.Value.EnviarRespostasAutomaticamente ?? false;
    }

    public Task<ConversaAtendimento?> ObterPorIdAsync(
        long conversaId,
        CancellationToken ct = default) =>
        _context.ConversasAtendimento
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.Id == conversaId, ct);

    public async Task<ConversaAtendimento> ObterOuCriarAsync(
        CanalAtendimento canal,
        string identificador,
        int? pacienteId = null,
        string? assunto = null,
        bool reativar = true,
        CancellationToken ct = default,
        bool resolverIdentidade = true)
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

            if (resolverIdentidade)
            {
                paciente ??= await _identidade.ResolverPacienteAsync(
                    canal,
                    normalizado,
                    ct);
            }

            AnexarPacienteExistente(paciente);

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

            // A desativação da identificação automática também vale para conversas
            // existentes. Apenas pacienteId explícito pode vincular nesse caso.
            // Não apague vínculos que já foram confirmados pela equipe.
            if (resolverIdentidade)
            {
                paciente ??= await _identidade.ResolverPacienteAsync(
                    canal,
                    normalizado,
                    ct);
            }

            AnexarPacienteExistente(paciente);

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
        catch (DbUpdateException ex)
            when (!string.IsNullOrWhiteSpace(externo) &&
                  EhDuplicidadeMensagemExterna(ex))
        {
            // Retry concorrente do mesmo webhook. Somente a violação do índice
            // único da mensagem externa é tratada como duplicidade; outros erros
            // de banco continuam subindo para diagnóstico.
            _context.Entry(mensagem).State = EntityState.Detached;
            return null;
        }

        if (conversa.Canal != CanalAtendimento.Web) await ApararAsync(conversa.Id, ct);

        return mensagem;
    }

    public async Task RegistrarInteracaoWebAsync(ConversaAtendimento conversa, string entrada, string resposta, CancellationToken ct = default)
    {
        if (conversa.Canal != CanalAtendimento.Web) throw new ArgumentException("A interação pertence ao chat do site.", nameof(conversa));
        var agora = DateTime.UtcNow;
        _context.MensagensAtendimento.AddRange(
            new MensagemAtendimento { ConversaAtendimentoId = conversa.Id, Direcao = DirecaoMensagemAtendimento.Entrada,
                Autor = AutorMensagemAtendimento.Paciente, Status = StatusMensagemAtendimento.Recebida,
                Texto = Limitar(entrada, 5000) ?? string.Empty, CriadoEm = agora },
            new MensagemAtendimento { ConversaAtendimentoId = conversa.Id, Direcao = DirecaoMensagemAtendimento.Saida,
                Autor = AutorMensagemAtendimento.Assistente, Status = StatusMensagemAtendimento.Enviada,
                Texto = Limitar(resposta, 5000) ?? string.Empty, CriadoEm = agora.AddTicks(1), EnviadoEm = agora });
        conversa.Ativa = true;
        conversa.AtualizadoEm = agora;
        conversa.UltimaInteracaoEm = agora;
        await _context.SaveChangesAsync(ct);
        // O chat Web/app possui histórico de atendimentos consultável pelo titular.
        // Limitar a leitura não deve apagar as conversas arquivadas.
    }

    private void AnexarPacienteExistente(Paciente? paciente)
    {
        if (paciente is null)
            return;

        // ResolverPacienteAsync usa AsNoTracking. Ao adicionar uma conversa nova,
        // um Paciente detached presente na navegação poderia ser interpretado pelo
        // EF como entidade nova e gerar INSERT/PK duplicada. Marca explicitamente
        // o paciente existente como Unchanged antes de anexar o grafo.
        var entry = _context.Entry(paciente);

        if (entry.State == EntityState.Detached)
            _context.Pacientes.Attach(paciente);
    }

    private static bool EhDuplicidadeMensagemExterna(DbUpdateException ex)
    {
        var postgres = ex.InnerException as PostgresException ??
                       ex.GetBaseException() as PostgresException;

        return postgres is not null &&
               postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
               string.Equals(
                   postgres.ConstraintName,
                   "IX_MensagensAtendimento_ConversaAtendimentoId_MensagemExternaId",
                   StringComparison.Ordinal);
    }

    public async Task<MensagemAtendimento> RegistrarSaidaAsync(
        ConversaAtendimento conversa,
        string texto,
        AutorMensagemAtendimento autor,
        StatusMensagemAtendimento status,
        string? autorUsuarioId = null,
        string? mensagemExternaId = null,
        string? erro = null,
        CancellationToken ct = default,
        string? chaveEnvio = null)
    {
        var agora = DateTime.UtcNow;

        var mensagem = new MensagemAtendimento
        {
            ConversaAtendimentoId = conversa.Id,
            ChaveEnvio = chaveEnvio,
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
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(chaveEnvio) &&
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_MensagensAtendimento_ChaveEnvio" })
        {
            _context.Entry(mensagem).State = EntityState.Detached;
            return await _context.MensagensAtendimento.AsNoTracking().SingleAsync(m => m.ChaveEnvio == chaveEnvio, ct);
        }
        if (conversa.Canal != CanalAtendimento.Web) await ApararAsync(conversa.Id, ct);

        return mensagem;
    }

    /// <summary>
    /// Apenas a última entrada ainda sem resposta pode ser retomada pela IA.
    /// Uma resposta da equipe/IA posterior (inclusive rascunho) impede duplicação.
    /// </summary>
    public async Task<MensagemAtendimento?> ObterUltimaEntradaPendenteAsync(
        long conversaId,
        CancellationToken ct = default)
    {
        var ultima = await _context.MensagensAtendimento.AsNoTracking()
            .Where(m => m.ConversaAtendimentoId == conversaId &&
                m.Direcao == DirecaoMensagemAtendimento.Entrada && m.Autor == AutorMensagemAtendimento.Paciente)
            .OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        if (ultima is null) return null;
        // Avisos do sistema, como "encaminhado para equipe", não respondem ao pedido.
        // Resposta humana, resposta da IA ou rascunho posterior já tratam a entrada.
        // Falha de transporte mantém a ação de reenvio da mesma mensagem;
        // somente um rascunho descartado libera geração de uma nova sugestão.
        var respondida = await _context.MensagensAtendimento.AsNoTracking().AnyAsync(m =>
            m.ConversaAtendimentoId == conversaId && m.Id > ultima.Id &&
            m.Direcao == DirecaoMensagemAtendimento.Saida &&
            m.Status != StatusMensagemAtendimento.Ignorada &&
            (m.Autor == AutorMensagemAtendimento.Funcionario || m.Autor == AutorMensagemAtendimento.Assistente), ct);
        return respondida ? null : ultima;
    }

    public async Task AtualizarEstadoIaAsync(ConversaAtendimento conversa, long entradaId, string estado, CancellationToken ct, bool manual = false)
    {
        if (estado == "analisando")
        {
            conversa.IaTentativas = conversa.IaEntradaId == entradaId ? conversa.IaTentativas + 1 : 1;
            if (conversa.Canal == CanalAtendimento.Email)
                conversa.IaPoliticaVersao = manual ? AtendimentoEmailOptions.PoliticaManual : AtendimentoEmailOptions.PoliticaAtual;
        }
        conversa.IaEntradaId = entradaId;
        conversa.IaEstado = estado;
        conversa.IaAtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }

    public Task RecarregarAsync(ConversaAtendimento conversa, CancellationToken ct) => _context.Entry(conversa).ReloadAsync(ct);

    public async Task<AtendimentoIaStatus> ObterEstadoIaAsync(ConversaAtendimento conversa, CancellationToken ct)
    {
        var entrada = await _context.MensagensAtendimento.AsNoTracking()
            .Where(m => m.ConversaAtendimentoId == conversa.Id && m.Direcao == DirecaoMensagemAtendimento.Entrada &&
                m.Autor == AutorMensagemAtendimento.Paciente)
            .OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        var saida = entrada is null ? null : await _context.MensagensAtendimento.AsNoTracking()
            .Where(m => m.ConversaAtendimentoId == conversa.Id && m.Id > entrada.Id &&
                m.Direcao == DirecaoMensagemAtendimento.Saida &&
                (m.Autor == AutorMensagemAtendimento.Assistente || m.Autor == AutorMensagemAtendimento.Funcionario))
            .OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        return AtendimentoIaStatus.Criar(conversa, entrada, saida, DateTime.UtcNow,
            _emailAutomatico && conversa.IaPoliticaVersao != AtendimentoEmailOptions.PoliticaManual);
    }

    public async Task<string> ChaveRascunhoAsync(long conversaId, long entradaId, CancellationToken ct)
    {
        var chave = $"ia:email:{conversaId}:{entradaId}";
        // Um rascunho descartado pode ser gerado de novo por solicitação explícita.
        var prefixo = chave + ":";
        var anterior = await _context.MensagensAtendimento.AsNoTracking()
            .Where(m => m.ChaveEnvio == chave || (m.ChaveEnvio != null && m.ChaveEnvio.StartsWith(prefixo)))
            .OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
        if (anterior is null) return chave;
        // A mesma tentativa usa a mesma chave até entre processos diferentes.
        return anterior.Status == StatusMensagemAtendimento.Ignorada
            ? $"{chave}:retry:{anterior.Id}" : anterior.ChaveEnvio!;
    }

    /// <summary>Somente esta conversa e falas efetivamente recebidas/enviadas antes da entrada atual.</summary>
    public async Task<IReadOnlyList<MensagemHistoricoAgente>> CarregarHistoricoEmailAsync(
        long conversaId, long antesDaEntradaId, CancellationToken ct = default)
    {
        var mensagens = await _context.MensagensAtendimento.AsNoTracking()
            .Where(m => m.ConversaAtendimentoId == conversaId && m.Id < antesDaEntradaId &&
                ((m.Direcao == DirecaoMensagemAtendimento.Entrada && m.Autor == AutorMensagemAtendimento.Paciente) ||
                 (m.Direcao == DirecaoMensagemAtendimento.Saida && m.Status == StatusMensagemAtendimento.Enviada &&
                  (m.Autor == AutorMensagemAtendimento.Assistente || m.Autor == AutorMensagemAtendimento.Funcionario))))
            .OrderByDescending(m => m.Id).Take(16).OrderBy(m => m.Id)
            .Select(m => new MensagemHistoricoAgente
            {
                Papel = m.Direcao == DirecaoMensagemAtendimento.Entrada ? "user" : "bot", Texto = m.Texto
            }).ToListAsync(ct);
        return AtendimentoEmailPolitica.PrepararHistorico(mensagens, limite: 16);
    }

    public async Task<IReadOnlyList<MensagemHistoricoAgente>>
        CarregarHistoricoPacienteAsync(
            int? pacienteId,
            long conversaAtualId,
            int limite = 24,
            CancellationToken ct = default,
            long? excluirMensagemId = null)
    {
        limite = Math.Clamp(limite, 1, 40);

        IQueryable<MensagemAtendimento> query =
            _context.MensagensAtendimento
                .AsNoTracking()
                .Include(m => m.Conversa)
                .Where(m => m.Status != StatusMensagemAtendimento.AguardandoAprovacao &&
                            m.Status != StatusMensagemAtendimento.Ignorada &&
                            (m.ChaveEnvio == null || !m.ChaveEnvio.StartsWith("feedback:")));

        // No processamento de retomada, a mensagem de entrada mais recente
        // ja e enviada separadamente ao agente. Evite repeti-la no historico.
        if (excluirMensagemId is long mensagemIdParaExcluir)
            query = query.Where(m => m.Id != mensagemIdParaExcluir);

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
                (m.Status == StatusMensagemAtendimento.Enviada || m.Status == StatusMensagemAtendimento.Entregue))
            .OrderBy(m => m.Id)
            .Take(30)
            .ToListAsync(ct);
    }

    private async Task ApararAsync(long conversaId, CancellationToken ct)
    {
        var ids = await _context.MensagensAtendimento
            .Where(m => m.ConversaAtendimentoId == conversaId && m.ChaveEnvio == null &&
                m.Status != StatusMensagemAtendimento.Processando && m.Status != StatusMensagemAtendimento.AguardandoEnvio && m.ProximaTentativaEm == null)
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
