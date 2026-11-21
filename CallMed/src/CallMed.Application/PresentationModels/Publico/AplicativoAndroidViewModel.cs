namespace MKSANCrud.ViewModels;

public sealed class AplicativoAndroidViewModel
{
    public bool Disponivel { get; set; }
    public string Versao { get; set; } = "";
    public int VersionCode { get; set; }
    public string AndroidMinimo { get; set; } = "8.0";
    public long TamanhoBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string TamanhoFormatado => (TamanhoBytes / 1024d / 1024d).ToString("0.0") + " MB";
}
