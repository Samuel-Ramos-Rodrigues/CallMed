namespace MKSANCrud.Models;

public static class TipoPagamentoConsulta
{
    public const string Particular = "Particular";
    public const string Convenio = "Convenio";

    public static string Normalizar(string? valor) =>
        string.Equals(valor?.Trim(), Convenio, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(valor?.Trim(), "Convênio", StringComparison.OrdinalIgnoreCase)
            ? Convenio
            : Particular;
}
