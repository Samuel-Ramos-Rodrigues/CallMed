namespace MKSANCrud.Models;

public static class ConsultaStatus
{
    public const string Pendente = "Pendente";
    public const string Confirmada = "Confirmada";
    public const string Remarcada = "Remarcada";
    public const string Cancelada = "Cancelada";
    public const string Realizada = "Realizada";
    public const string Ausente = "Ausente";

    public static readonly string[] Todos =
    [
        Pendente,
        Confirmada,
        Remarcada,
        Cancelada,
        Realizada,
        Ausente
    ];

    public static bool EhAtiva(string? status) =>
        !string.Equals(status, Cancelada, StringComparison.OrdinalIgnoreCase);

    public static bool PodeRemarcar(string? status) =>
        string.Equals(status, Pendente, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Confirmada, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Remarcada, StringComparison.OrdinalIgnoreCase);

    public static bool PodeConfirmar(string? status) =>
        string.Equals(status, Pendente, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Remarcada, StringComparison.OrdinalIgnoreCase);

    public static bool PodeCancelar(string? status) => PodeRemarcar(status);

    public static bool PodeRealizar(string? status) =>
        string.Equals(status, Confirmada, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Remarcada, StringComparison.OrdinalIgnoreCase);

    public static bool PodeMarcarAusente(string? status) =>
        string.Equals(status, Confirmada, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Remarcada, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Pendente, StringComparison.OrdinalIgnoreCase);
}
