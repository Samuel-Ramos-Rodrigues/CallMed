namespace MKSANCrud.Options;

public sealed class AtendimentoEmailOptions
{
    public const string SectionName = "Atendimento:Email";
    public const string PoliticaAtual = "email-auto-v1";
    public const string PoliticaManual = "email-manual-v1";
    public bool EnviarRespostasAutomaticamente { get; set; } = true;
    public bool RetomarPendencias { get; set; } = true;
    public int JanelaPendenciasHoras { get; set; } = 24;
    public string PortalBaseUrl { get; set; } = string.Empty;

    public string? UrlPortal(string caminho)
    {
        if (!Uri.TryCreate(PortalBaseUrl.Trim(), UriKind.Absolute, out var origin) ||
            (origin.Scheme != Uri.UriSchemeHttps && !(origin.IsLoopback && origin.Scheme == Uri.UriSchemeHttp)) ||
            !string.IsNullOrEmpty(origin.UserInfo)) return null;
        return new Uri(origin.GetLeftPart(UriPartial.Authority) + "/" + caminho.TrimStart('/')).AbsoluteUri;
    }
}
