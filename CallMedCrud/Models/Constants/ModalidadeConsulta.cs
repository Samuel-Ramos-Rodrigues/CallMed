namespace MKSANCrud.Models;

public static class ModalidadeConsulta
{
    public const string Presencial = "Presencial";
    public const string Online = "Online";

    public static string Normalizar(string? valor) =>
        string.Equals(valor, Online, StringComparison.OrdinalIgnoreCase) ? Online : Presencial;
}
