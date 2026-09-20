using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Models;
using MKSANCrud.Options;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
var paciente = new Paciente { Nome = "Paciente de teste", Cpf = "52998224725", Email = null };
Check(Validator.TryValidateObject(paciente, new ValidationContext(paciente), [], true), "Cadastro administrativo aceita e-mail ausente");
paciente.Email = "invalido";
Check(!Validator.TryValidateObject(paciente, new ValidationContext(paciente), [], true), "E-mail preenchido inválido é rejeitado");
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["Clinica:Endereco"] = "Endereço de teste", ["Clinica:Orientacoes:AntecedenciaMinutos"] = "20",
    ["Clinica:Orientacoes:PorEspecialidade:1"] = "Orientação administrativa da especialidade."
}).Build();
var service = new OrientacoesConsultaService(configuration);
var consulta = new Consulta { TipoPagamento = TipoPagamentoConsulta.Convenio, Medico = new Medico { EspecialidadeId = 1 } };
Check(service.Texto(consulta).Contains("20 minutos"), "Antecedência usa configuração da clínica");
Check(service.Texto(consulta).Contains("carteirinha"), "Consulta por convênio inclui documentação");
Check(service.Texto(consulta).Contains("Orientação administrativa da especialidade."), "Orientação por especialidade é incluída");
consulta.TipoPagamento = TipoPagamentoConsulta.Particular;
Check(!service.Texto(consulta).Contains("carteirinha"), "Consulta particular não exige carteirinha");
var options = Options.Create(new EvolutionWhatsAppOptions { Enabled = true, BaseUrl = "https://evolution.test", ApiKey = "test-only", InstanceName = "callmed" });
var handler = new FakeHandler();
var sender = new EvolutionWhatsAppSender(new HttpClient(handler), options, NullLogger<EvolutionWhatsAppSender>.Instance);
handler.Body = "{\"instance\":{\"state\":\"open\"}}";
Check((await sender.VerificarConexaoAsync()).Conectado, "Evolution open reconhecido");
Check(handler.LastPath == "/instance/connectionState/callmed" && handler.LastMethod == HttpMethod.Get, "Diagnóstico consulta a instância sem enviar mensagens");
handler.Body = "{\"instance\":{\"state\":\"close\"}}";
Check(!(await sender.VerificarConexaoAsync()).Conectado, "Evolution desconectada não aparece como conectada");
handler.Status = HttpStatusCode.Unauthorized;
Check(!(await sender.VerificarConexaoAsync()).Conectado, "Chave inválida não aparece como conexão ativa");
handler.Status = HttpStatusCode.OK; handler.Body = "invalid";
Check(!(await sender.VerificarConexaoAsync()).Conectado, "Resposta inválida é tratada");
Console.WriteLine("Verificações concluídas. Nenhuma mensagem real foi enviada.");
sealed class FakeHandler : HttpMessageHandler
{
    public string Body = "{}";
    public HttpStatusCode Status = HttpStatusCode.OK;
    public string? LastPath;
    public HttpMethod? LastMethod;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastPath = request.RequestUri!.AbsolutePath; LastMethod = request.Method;
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
    }
}
