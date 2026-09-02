using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

public sealed record ResultadoElegibilidadeConvenio(
    bool PossuiConvenioValido,
    bool RegrasConfiguradas,
    bool Elegivel,
    string Mensagem);

public sealed class ConvenioElegibilidadeService
{
    private readonly MKSANContext _context;
    private readonly ConvenioService _convenio;

    public ConvenioElegibilidadeService(MKSANContext context, ConvenioService convenio)
    {
        _context = context;
        _convenio = convenio;
    }

    public async Task<ResultadoElegibilidadeConvenio> AvaliarAsync(
        Paciente paciente,
        int? especialidadeId,
        CancellationToken ct = default)
    {
        if (!_convenio.EhValido(paciente))
            return new(false, false, true, "Paciente sem convênio válido; atendimento particular disponível.");

        if (!especialidadeId.HasValue || especialidadeId.Value <= 0)
            return new(true, false, true, "Convênio válido. Selecione a especialidade para validar a cobertura.");

        var chave = NormalizarChave(paciente.NomeConvenio);
        if (string.IsNullOrWhiteSpace(chave))
            return new(true, false, true, "Convênio válido, mas o nome do convênio precisa ser revisado.");

        var regras = await _context.ConveniosEspecialidades
            .AsNoTracking()
            .Where(x => x.Ativo && x.ConvenioChave == chave)
            .ToListAsync(ct);

        if (regras.Count == 0)
        {
            // Sem matriz, a elegibilidade precisa ser resolvida na triagem ou por uma liberação manual justificada.
            // O serviço apenas informa o estado; os fluxos clínicos decidem se podem prosseguir.
            return new(true, false, true, "Convênio válido; não há matriz de cobertura cadastrada para este convênio.");
        }

        var regra = regras.FirstOrDefault(x => x.EspecialidadeId == especialidadeId.Value);
        if (regra is null || !regra.Coberta)
            return new(true, true, false, "A especialidade selecionada não está coberta pelas regras cadastradas deste convênio.");

        return new(true, true, true, "Convênio válido e especialidade coberta.");
    }

    public async Task<ConvenioEspecialidade> SalvarRegraAsync(
        string convenioNome,
        int especialidadeId,
        bool coberta,
        string? observacao,
        CancellationToken ct = default)
    {
        var nome = (convenioNome ?? string.Empty).Trim();
        var chave = NormalizarChave(nome);
        if (string.IsNullOrWhiteSpace(chave))
            throw new InvalidOperationException("Informe o nome do convênio.");

        if (!await _context.Especialidades.AsNoTracking().AnyAsync(x => x.Id == especialidadeId, ct))
            throw new InvalidOperationException("Especialidade não encontrada.");

        var regra = await _context.ConveniosEspecialidades
            .FirstOrDefaultAsync(x => x.ConvenioChave == chave && x.EspecialidadeId == especialidadeId, ct);

        if (regra is null)
        {
            regra = new ConvenioEspecialidade
            {
                ConvenioNome = nome,
                ConvenioChave = chave,
                EspecialidadeId = especialidadeId,
                CriadoEm = DateTime.UtcNow
            };
            _context.ConveniosEspecialidades.Add(regra);
        }

        regra.ConvenioNome = nome;
        regra.Coberta = coberta;
        regra.Ativo = true;
        regra.Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        regra.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return regra;
    }

    public static string NormalizarChave(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var espaco = false;
        foreach (var c in texto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                espaco = false;
            }
            else if (!espaco && sb.Length > 0)
            {
                sb.Append(' ');
                espaco = true;
            }
        }
        return sb.ToString().Trim().Normalize(NormalizationForm.FormC);
    }
}
