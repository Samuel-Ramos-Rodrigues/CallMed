using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MKSANCrud.Services.Clinica;

internal static class TeleconsultaIceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var data=new Dictionary<string,string?>();
        var config=new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var service=new TeleconsultaIceService(config,new Clock());
        check(service.Criar() is {Erro:null,Servidores.Count:0},"Configuração vazia mantém conexão direta sem inventar credenciais TURN");
        config["Clinica:Tele:IceUrls:0"]="stun:stun.ficticio.invalid:80";
        check(service.Criar() is {Erro:null,Servidores.Count:1}&&service.Criar().Servidores[0].Username==null,"STUN não recebe usuário ou senha TURN");
        config["Clinica:Tele:IceUrls:1"]="turn:relay.ficticio.invalid:80";
        check(service.Criar().Erro!=null,"TURN sem autenticação não é liberado silenciosamente");
        config["Clinica:Tele:TurnUsername"]="usuario-ficticio";
        check(service.Criar().Erro!=null,"Usuário sem senha retorna erro de configuração");
        config["Clinica:Tele:TurnPassword"]="senha-TURN-ficticia";
        config["Clinica:Tele:IceUrls:2"]="turn:relay.ficticio.invalid:80?transport=tcp";
        config["Clinica:Tele:IceUrls:3"]="turn:relay.ficticio.invalid:443";
        config["Clinica:Tele:IceUrls:4"]="turns:relay.ficticio.invalid:443?transport=tcp";
        var r=service.Criar();
        check(r.Erro==null&&r.Servidores.Count==2&&r.Servidores[1].Username=="usuario-ficticio"&&r.Servidores[1].Credential=="senha-TURN-ficticia","Provedor recebe exatamente username e credential configurados");
        check(r.Servidores[1].Urls.Length==4&&r.Servidores[1].Urls[1].EndsWith("?transport=tcp")&&r.Servidores[1].Urls[3].StartsWith("turns:"),"Lista preserva UDP, TCP e TLS fornecidos pelo provedor");
        config["Clinica:Tele:TurnSecret"]="segredo-coturn-ficticio";
        check(service.Criar().Servidores[1].Username=="usuario-ficticio","Credencial explícita do provedor tem prioridade sobre segredo coturn antigo");
        config["Clinica:Tele:TurnUsername"]="";
        check(service.Criar().Erro!=null,"Credencial incompleta não muda silenciosamente para autenticação coturn");
        config["Clinica:Tele:TurnPassword"]="";
        r=service.Criar();var turn=r.Servidores[1];
        var expected=Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(config["Clinica:Tele:TurnSecret"]!),Encoding.UTF8.GetBytes(turn.Username!)));
        check(r.Erro==null&&turn.Username!.StartsWith(new Clock().GetUtcNow().AddHours(5).ToUnixTimeSeconds()+":")&&turn.Credential==expected,"Coturn mantém credencial temporária HMAC com validade de cinco horas");
        var json=JsonSerializer.Serialize(r.Servidores,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        check(!json.Contains("segredo-coturn-ficticio")&&!json.Contains("\"credential\":null"),"Resposta não expõe segredo compartilhado nem credenciais nulas de STUN");
        config["Clinica:Tele:IceUrls:5"]="https://nao-e-endereco-ice.invalid";
        check(service.Criar().Erro!=null,"Endereço HTTP no lugar de STUN/TURN é recusado");
    }
    sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(2026,11,7,20,0,0,TimeSpan.Zero);}
}
