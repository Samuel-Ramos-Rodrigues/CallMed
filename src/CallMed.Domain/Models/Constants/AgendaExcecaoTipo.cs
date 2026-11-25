namespace MKSANCrud.Models;

public static class AgendaExcecaoTipo
{
    public const string Bloqueio = "Bloqueio";
    public const string Encaixe = "Encaixe";
    public const string Ausencia = "Ausencia";

    public static readonly string[] Todos = [Bloqueio, Encaixe, Ausencia];
}
