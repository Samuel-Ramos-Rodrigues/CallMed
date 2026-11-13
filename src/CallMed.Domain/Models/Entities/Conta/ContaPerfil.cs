namespace MKSANCrud.Models;

public sealed class ContaPerfil
{
    public string UsuarioId { get; set; } = "";
    public string? NomeExibicao { get; set; }
    public string? Telefone { get; set; }
    public bool PreferenciasDefinidas { get; set; }
    public string Tema { get; set; } = "light";
    public bool TextoMaior { get; set; }
    public bool AltoContraste { get; set; }
    public bool ReduzirAnimacoes { get; set; }
    public bool ModoSimplificado { get; set; }
    public bool AvisosWhatsApp { get; set; } = true;
    public bool AvisosEmail { get; set; } = true;
    public bool AvisosSms { get; set; } = true;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
public sealed class ContaFoto
{
    public string UsuarioId { get; set; } = "";
    public byte[] Conteudo { get; set; } = [];
    public string Tipo { get; set; } = "image/jpeg";
    public Guid Versao { get; set; } = Guid.NewGuid();
}
public sealed class ContaSessao
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UsuarioId { get; set; } = "";
    public string Dispositivo { get; set; } = "Navegador";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? RevogadaEm { get; set; }
    public Guid RefreshVersion { get; set; } = Guid.NewGuid();
}
public sealed class ContaEvento
{
    public long Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public string Descricao { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
