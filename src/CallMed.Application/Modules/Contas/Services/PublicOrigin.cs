namespace MKSANCrud.Services.Usuarios;

public static class PublicOrigin
{
    // Jobs e canais externos não devem gerar links inválidos quando a origem não está configurada.
    // A origem oficial configurada tem prioridade sobre a URL de um serviço/worker do Render.
    public static Uri? TentarObter(IConfiguration config)
    {
        // A origem explícita prevalece; só aceita os aliases legados se ela estiver ausente.
        // Isso preserva os testes e instalações antigas sem permitir que a URL do worker
        // sobrescreva o endereço público quando PublicOrigin já foi definido.
        if (!string.IsNullOrWhiteSpace(config["PublicOrigin"]) ||
            !string.IsNullOrWhiteSpace(config["Atendimento:Email:PortalBaseUrl"]))
        {
            try { return Obter(config); }
            catch (InvalidOperationException) { return null; }
        }
        foreach (var key in new[] { "Twa:Origin", "RENDER_EXTERNAL_URL" })
        {
            var value = config[key];
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 &&
                uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0)
                return uri;
        }
        return null;
    }

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
