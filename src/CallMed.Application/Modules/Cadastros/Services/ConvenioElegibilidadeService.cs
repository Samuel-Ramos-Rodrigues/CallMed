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
    string Mensagem,
    string StatusConsulta,
    bool RequerAutorizacao,
    string? ObservacaoConsulta);

/// <summary>
/// Aplica a matriz administrativa da clínica. Ela nunca afirma validar o beneficiário em tempo real:
/// a elegibilidade individual precisa ser confirmada junto à operadora antes da realização do atendimento.
/// </summary>
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
            return new(false, false, true, "Paciente sem convênio válido; atendimento particular disponível.",
                ElegibilidadeConvenioStatus.NaoAplicavel, false, null);

        if (!especialidadeId.HasValue || especialidadeId.Value <= 0)
            return Pendente(false, "Convênio cadastrado. Selecione a especialidade para consultar a matriz de cobertura.",
                "A cobertura ainda não pode ser avaliada porque não há especialidade selecionada.");

        var chave = NormalizarChave(paciente.NomeConvenio);
        if (string.IsNullOrWhiteSpace(chave))
            return Pendente(false, "O nome do convênio precisa ser revisado no cadastro do paciente.",
                "Cadastro do convênio sem nome normalizável; confirmar com o paciente e a operadora.");

        var regra = await _context.ConveniosEspecialidades
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Ativo && x.ConvenioChave == chave && x.EspecialidadeId == especialidadeId.Value, ct);

        if (regra is null)
            return Pendente(false, "Não há regra específica para este convênio e especialidade. A consulta pode ser agendada, mas a equipe deve confirmar a cobertura com a operadora.",
                "Sem regra específica na matriz CallMed. Confirmar vigência do plano, rede credenciada, especialidade e necessidade de autorização junto à operadora.");

        var statusCobertura = string.IsNullOrWhiteSpace(regra.StatusCobertura)
            ? (regra.Coberta ? StatusCoberturaConvenio.Coberta : StatusCoberturaConvenio.NaoCoberta)
            : regra.StatusCobertura;

        if (string.Equals(statusCobertura, StatusCoberturaConvenio.NaoCoberta, StringComparison.OrdinalIgnoreCase))
            return new(true, true, false, "A matriz da clínica registra esta especialidade como não coberta pelo convênio. Você ainda pode agendar escolhendo atendimento particular.",
                ElegibilidadeConvenioStatus.NaoCoberta, false, "A matriz CallMed registra não cobertura para esta combinação de convênio e especialidade. Se a informação estiver desatualizada, a equipe deve confirmar o contrato com a operadora.");

        if (string.Equals(statusCobertura, StatusCoberturaConvenio.VerificacaoManual, StringComparison.OrdinalIgnoreCase))
            return new(true, true, true, "A regra exige conferência manual. A consulta será confirmada, e a equipe verificará cobertura e elegibilidade junto à operadora.",
                ElegibilidadeConvenioStatus.PendenteValidacao, regra.RequerAutorizacao, regra.Observacao ?? "A matriz indica conferência manual; validar plano, rede e especialidade antes do atendimento.");

        if (!string.Equals(statusCobertura, StatusCoberturaConvenio.Coberta, StringComparison.OrdinalIgnoreCase))
            return Pendente(true, "A situação cadastrada na matriz não pôde ser reconhecida. A equipe precisa revisar a regra antes do atendimento.",
                "Valor de StatusCobertura desconhecido. Não assumir cobertura sem revisar a regra.");

        if (regra.RequerAutorizacao)
            return new(true, true, true, "A matriz indica cobertura, mas exige autorização. A consulta pode ser reservada; a autorização deve ser acompanhada antes do atendimento.",
                ElegibilidadeConvenioStatus.AguardandoAutorizacao, true, regra.Observacao ?? "A matriz requer autorização prévia. Registrar protocolo e resultado da operadora antes da realização.");

        return new(true, true, true, "A matriz da clínica indica cobertura para esta especialidade. A elegibilidade ativa do beneficiário ainda deve ser confirmada com a operadora antes do atendimento.",
            ElegibilidadeConvenioStatus.CoberturaNaMatriz, false, regra.Observacao ?? "A matriz CallMed indica cobertura; conferir elegibilidade ativa, produto/plano e rede credenciada junto à operadora.");
    }

    public async Task<ConvenioEspecialidade> SalvarRegraAsync(
        string convenioNome,
        int especialidadeId,
        string statusCobertura,
        bool requerAutorizacao,
        string? observacao,
        CancellationToken ct = default)
    {
        var nome = (convenioNome ?? string.Empty).Trim();
        var chave = NormalizarChave(nome);
        if (string.IsNullOrWhiteSpace(chave))
            throw new InvalidOperationException("Informe o nome do convênio.");

        var status = StatusCoberturaConvenio.Todos.FirstOrDefault(x =>
            x.Equals(statusCobertura?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null)
            throw new InvalidOperationException("Selecione uma situação de cobertura válida.");

        if (!await _context.Especialidades.AsNoTracking().AnyAsync(x => x.Id == especialidadeId && x.Ativo, ct))
            throw new InvalidOperationException("Especialidade não encontrada ou inativa.");

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
        regra.StatusCobertura = status;
        // Mantém o campo legado coerente para versões/consultas antigas do sistema.
        regra.Coberta = status != StatusCoberturaConvenio.NaoCoberta;
        regra.RequerAutorizacao = status == StatusCoberturaConvenio.Coberta && requerAutorizacao;
        regra.Ativo = true;
        regra.Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        regra.AtualizadoEm = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return regra;
    }

    private static ResultadoElegibilidadeConvenio Pendente(bool regrasConfiguradas, string mensagem, string observacao) =>
        new(true, regrasConfiguradas, true, mensagem, ElegibilidadeConvenioStatus.PendenteValidacao, false, observacao);

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
