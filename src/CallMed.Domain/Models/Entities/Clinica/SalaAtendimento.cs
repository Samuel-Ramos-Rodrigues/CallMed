namespace MKSANCrud.Models;

/// <summary>Sala reservada pela jornada de consultas.</summary>
public sealed class SalaAtendimento
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Localizacao {get;set;}="";
    public string Recursos {get;set;}="";
    public bool Ativa { get; set; } = true;
}
