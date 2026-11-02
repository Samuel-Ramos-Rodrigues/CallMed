using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;

internal static class EvolutionDiagnosticoChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var http = new Handler();
        var sender = Sender(http, "  https://evolution.test/gateway/evo/  ", "  CallMed Centro  ");
        var diag = await sender.DiagnosticarAsync();
        check(diag.Conectado && diag.StatusHttp == 200 && http.Method == HttpMethod.Get && http.Calls == 1,
            "Diagnóstico Evolution consulta por GET e informa o status HTTP sem enviar mensagens");
        check(http.Url == "https://evolution.test/gateway/evo/instance/connectionState/CallMed%20Centro" &&
              diag.Instancia == "CallMed Centro",
            "URL e instância removem espaços externos, preservando maiúsculas, espaços internos e prefixo do proxy");
        check(diag.UrlConsulta == http.Url &&
              diag.UrlEnvio == "https://evolution.test/gateway/evo/message/sendText/CallMed%20Centro" &&
              !JsonSerializer.Serialize(diag).Contains("chave-teste-privada"),
            "Diagnóstico expõe apenas URLs efetivas e instância, sem chave API");
        var enviado = await sender.EnviarAsync("5579999990000", "teste sem envio real");
        check(enviado.Sucesso && http.Method == HttpMethod.Post && http.Url == diag.UrlEnvio &&
              http.ApiKey == "chave-teste-privada" &&
              JsonDocument.Parse(http.Body!).RootElement.GetProperty("text").GetString() == "teste sem envio real",
            "Consulta e envio usam a mesma instância normalizada e preservam autenticação e payload v2");

        http = new Handler { Status = HttpStatusCode.NotFound,
            Response = """{"status":404,"response":{"message":["The \"privada\" instance does not exist"]},"apikey":"dado-privado","number":"5579999990000"}""" };
        sender = Sender(http);
        diag = await sender.DiagnosticarAsync();
        var falha = await sender.EnviarAsync("5579999990000", "texto-privado");
        check(!diag.Conectado && diag.StatusHttp == 404 && diag.Mensagem.Contains("instância não existe") &&
              falha.Erro == diag.Mensagem && http.Calls == 2,
            "404 de instância é identificado em consulta e envio, sem tentar outra rota ou payload");
        check(!diag.Mensagem.Contains("privada") && !falha.Erro!.Contains("dado-privado") &&
              !falha.Erro.Contains("5579999990000") && !falha.Erro.Contains("texto-privado"),
            "Erro Evolution não repassa corpo do provedor nem dados privados");
        http.Response = """{"success":false,"error":{"code":"NOT_FOUND","message":"Instance not found"}}""";
        check((await sender.DiagnosticarAsync()).Mensagem.Contains("instância não existe"),
            "Diagnóstico reconhece também a mensagem de instância no envelope error");
        http.Response = """{"message":"Cannot POST /gateway/message/sendText/callmed"}""";
        check((await sender.EnviarAsync("5579999990000", "teste")).Erro!.Contains("não reconheceu a rota"),
            "404 de rota é distinguido de instância ausente");
        http.Response = "<!doctype html><html><body><pre>Cannot GET /instance/connectionState/callmed</pre></body></html>";
        check((await sender.DiagnosticarAsync()).Mensagem.Contains("não reconheceu a rota"),
            "404 HTML com Cannot GET identifica rota não reconhecida");
        http.Response = "<html><body>Not Found</body></html>";
        check((await sender.DiagnosticarAsync()).Mensagem.Contains("página HTML"),
            "404 de página HTML orienta verificar API e proxy");
        http.Response = """{"error":"Not Found","meta":{"path":"/instance/connectionState/callmed"}}""";
        check((await sender.DiagnosticarAsync()).Mensagem.Contains("sem identificar"),
            "404 genérico não atribui causa com base no caminho da requisição");
        http.Status = HttpStatusCode.Unauthorized;
        check((await sender.DiagnosticarAsync()).Mensagem.Contains("autenticação"),
            "401 é tratado como falha de autenticação e acesso");
        http.Status = HttpStatusCode.OK;
        http.Response = "<html>Manager</html>";
        diag = await sender.DiagnosticarAsync();
        check(!diag.Conectado && diag.StatusHttp == 200 && diag.Mensagem.Contains("JSON esperado"),
            "HTTP 200 com página do painel não comprova conexão e mantém o status da resposta");

        foreach (var url in new[] {
            "https://evolution.test/manager/", "https://evolution.test/message/sendText/callmed",
            "https://evolution.test/instance/connectionState/callmed", "https://evolution.test?secret=privado",
            "https://evolution.test/#manager", "https://usuario:senha@evolution.test", "ftp://evolution.test"
        })
        {
            http = new Handler(); sender = Sender(http, url);
            diag = await sender.DiagnosticarAsync();
            falha = await sender.EnviarAsync("5579999990000", "teste");
            check(!sender.Configurado && !diag.Conectado && !falha.Sucesso && http.Calls == 0 &&
                  diag.UrlConsulta is null && diag.UrlEnvio is null && !diag.Mensagem.Contains("privado"),
                $"BaseUrl inválida é rejeitada antes da requisição ({new Uri(url).AbsolutePath})");
        }
    }

    private static EvolutionWhatsAppSender Sender(Handler http, string url = "https://evolution.test", string instance = "callmed")
    {
        var options = Options.Create(new EvolutionWhatsAppOptions {
            Enabled = true, BaseUrl = url, InstanceName = instance, ApiKey = "chave-teste-privada", IntervaloMinimoEnvioMs = 0
        });
        return new(new HttpClient(http), options, NullLogger<EvolutionWhatsAppSender>.Instance,
            new EvolutionEnvioControle(TimeProvider.System, options));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string Response = """{"instance":{"state":"open"},"key":{"id":"teste"}}""";
        public int Calls;
        public string? Url, ApiKey, Body;
        public HttpMethod? Method;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Url = request.RequestUri!.AbsoluteUri; Method = request.Method;
            ApiKey = request.Headers.GetValues("apikey").Single();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new(Status) { Content = new StringContent(Response) };
        }
    }
}
