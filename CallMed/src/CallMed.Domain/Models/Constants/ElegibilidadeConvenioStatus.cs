namespace MKSANCrud.Models;

/// <summary>Estados operacionais da conferência do convênio. A matriz local não substitui a confirmação junto à operadora.</summary>
public static class ElegibilidadeConvenioStatus
{
    public const string NaoAplicavel = "NaoAplicavel";
    public const string PendenteValidacao = "PendenteValidacao";
    public const string CoberturaNaMatriz = "CoberturaNaMatriz";
    public const string AguardandoAutorizacao = "AguardandoAutorizacao";
    public const string Validada = "Validada";
    public const string NaoCoberta = "NaoCoberta";

    public static readonly string[] Todos =
    [
        NaoAplicavel,
        PendenteValidacao,
        CoberturaNaMatriz,
        AguardandoAutorizacao,
        Validada,
        NaoCoberta
    ];

    public static bool RequerRevisao(string? status) =>
        string.Equals(status, PendenteValidacao, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, CoberturaNaMatriz, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, AguardandoAutorizacao, StringComparison.OrdinalIgnoreCase);

    public static string Rotulo(string? status) => status switch
    {
        PendenteValidacao => "Pendente de conferência",
        CoberturaNaMatriz => "Matriz indica cobertura — conferir operadora",
        AguardandoAutorizacao => "Aguardando autorização",
        Validada => "Elegibilidade validada",
        NaoCoberta => "Convênio não cobre — ajustado para particular",
        _ => "Não se aplica"
    };
}

public static class StatusCoberturaConvenio
{
    public const string Coberta = "Coberta";
    public const string NaoCoberta = "NaoCoberta";
    public const string VerificacaoManual = "VerificacaoManual";

    public static readonly string[] Todos = [Coberta, NaoCoberta, VerificacaoManual];

    public static string Rotulo(string? status) => status switch
    {
        Coberta => "Coberta pela matriz",
        NaoCoberta => "Não coberta",
        _ => "Conferência manual necessária"
    };
}
