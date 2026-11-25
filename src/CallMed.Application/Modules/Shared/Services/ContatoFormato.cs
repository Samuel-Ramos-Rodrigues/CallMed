namespace MKSANCrud.Services.Clinica;
public static class ContatoFormato
{
    public static string Cpf(string? value) { var n=new string((value??"").Where(char.IsDigit).ToArray()); return n.Length==11?$"{n[..3]}.{n[3..6]}.{n[6..9]}-{n[9..]}":value??"—"; }
    public static string Telefone(string? value) { var n=new string((value??"").Where(char.IsDigit).ToArray()); if(n.StartsWith("55")&&n.Length is 12 or 13)n=n[2..]; return n.Length==11?$"({n[..2]}) {n[2..7]}-{n[7..]}":n.Length==10?$"({n[..2]}) {n[2..6]}-{n[6..]}":value??"—"; }
}
