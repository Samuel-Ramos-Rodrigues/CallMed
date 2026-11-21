namespace MKSANCrud.Services.Usuarios;

public static class PublicOrigin
{
    public static Uri Obter(IConfiguration config)
    {
        var value = config["PublicOrigin"];
        if (string.IsNullOrWhiteSpace(value)) value = config["Atendimento:Email:PortalBaseUrl"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin) || origin.Scheme != "https" ||
            !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new InvalidOperationException("Configure PublicOrigin com a origem HTTPS oficial da CallMed, sem caminho, parâmetros ou credenciais.");
        return origin;
    }
}
