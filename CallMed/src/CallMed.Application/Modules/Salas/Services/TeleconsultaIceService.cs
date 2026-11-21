using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace MKSANCrud.Services.Clinica;

public sealed class TeleconsultaIceService(IConfiguration config, TimeProvider time)
{
    public TeleconsultaIceResult Criar()
    {
        var urls=(config.GetSection("Clinica:Tele:IceUrls").Get<string[]>()??[])
            .Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if(urls.Any(x=>!x.StartsWith("stun:",StringComparison.Ordinal)&&!x.StartsWith("turn:",StringComparison.Ordinal)&&!x.StartsWith("turns:",StringComparison.Ordinal)))
            return new([],"A clínica precisa conferir os endereços do serviço de vídeo.");
        var servers=new List<TeleconsultaIceServer>();
        var stun=urls.Where(x=>x.StartsWith("stun:",StringComparison.Ordinal)).ToArray();
        if(stun.Length>0)servers.Add(new(stun));
        var turn=urls.Where(x=>x.StartsWith("turn:",StringComparison.Ordinal)||x.StartsWith("turns:",StringComparison.Ordinal)).ToArray();
        if(turn.Length==0)return new(servers,null);
        var username=config["Clinica:Tele:TurnUsername"];
        var password=config["Clinica:Tele:TurnPassword"];
        if(!string.IsNullOrWhiteSpace(username)||!string.IsNullOrWhiteSpace(password))
        {
            if(string.IsNullOrWhiteSpace(username)||string.IsNullOrWhiteSpace(password))
                return new([],"A clínica precisa completar as credenciais do serviço de vídeo.");
            // Credenciais TURN do provedor, como as exibidas pelo Metered.
            servers.Add(new(turn,username,password));
        }
        else
        {
            var secret=config["Clinica:Tele:TurnSecret"];
            if(string.IsNullOrWhiteSpace(secret))return new([],"A clínica precisa configurar as credenciais do serviço de vídeo.");
            // Compatibilidade com coturn configurado com use-auth-secret.
            var temporary=$"{time.GetUtcNow().AddHours(5).ToUnixTimeSeconds()}:{Guid.NewGuid():N}";
            var credential=Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(temporary)));
            servers.Add(new(turn,temporary,credential));
        }
        return new(servers,null);
    }
}

public sealed record TeleconsultaIceResult(IReadOnlyList<TeleconsultaIceServer> Servidores,string? Erro);
public sealed record TeleconsultaIceServer(string[] Urls,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Username=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Credential=null);
