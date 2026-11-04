using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using MKSANCrud.Services.Atendimento;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MKSANCrud.Models;
using MKSANCrud.Options;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Atendimento.Canais.WhatsApp;
using MKSANCrud.Services.Atendimento.Canais.Email;
using MKSANCrud.Services.Email;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
var webhookSemHeader = new DefaultHttpContext();
webhookSemHeader.Request.QueryString = new QueryString("?secret=segredo-teste");
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Webhook aceita segredo em query string quando não há cabeçalhos");
webhookSemHeader.Request.Headers["X-CallMed-Webhook-Secret"] = "segredo-teste";
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Webhook aceita segredo correto no cabeçalho CallMed");
webhookSemHeader.Request.Headers.Remove("X-CallMed-Webhook-Secret");
webhookSemHeader.Request.Headers["X-MKSAN-Webhook-Secret"] = "segredo-teste";
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Webhook mantém compatibilidade com cabeçalho legado");
webhookSemHeader.Request.Headers["X-MKSAN-Webhook-Secret"] = "incorreto";
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Webhook rejeita segredo incorreto");
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, ""), "Webhook nega acesso quando segredo não está configurado");
webhookSemHeader.Request.Headers["X-CallMed-Webhook-Secret"] = "incorreto";
webhookSemHeader.Request.Headers["X-MKSAN-Webhook-Secret"] = "segredo-teste";
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "CallMed incorreto não pode ser substituído pelo legado ou pela query corretos");
webhookSemHeader.Request.Headers["X-CallMed-Webhook-Secret"] = "segredo-teste";
webhookSemHeader.Request.Headers["X-MKSAN-Webhook-Secret"] = "incorreto";
webhookSemHeader.Request.QueryString = new QueryString("?secret=incorreto");
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Cabeçalho CallMed correto tem prioridade sobre legado e query incorretos");
webhookSemHeader.Request.Headers["X-CallMed-Webhook-Secret"] = " ";
webhookSemHeader.Request.Headers["X-MKSAN-Webhook-Secret"] = "segredo-teste";
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "CallMed vazio permite usar cabeçalho legado antes da query");
webhookSemHeader.Request.Headers["X-MKSAN-Webhook-Secret"] = " ";
webhookSemHeader.Request.QueryString = new QueryString("?secret=segredo-teste");
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Cabeçalhos em branco permitem o fallback para a query");
webhookSemHeader.Request.Headers.Clear();
webhookSemHeader.Request.QueryString = new QueryString("?secret=incorreto");
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Query incorreta não autentica o webhook");
webhookSemHeader.Request.QueryString = QueryString.Empty;
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Webhook rejeita requisição sem segredo em nenhuma fonte");
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, null), "Webhook rejeita configuração nula");
webhookSemHeader.Request.QueryString = new QueryString("?secret=segredo-teste&secret=outro");
Check(!AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-teste"), "Query com valores duplicados não é aceita como segredo único");
webhookSemHeader.Request.QueryString = QueryString.Create("secret", "segredo-ação-🔒");
Check(AtendimentoWebhookSecretValidator.Validar(webhookSemHeader.Request, "segredo-ação-🔒"), "Webhook compara corretamente segredo UTF-8 recebido pela query");
var rejeitouRequestNulo = false;
try { AtendimentoWebhookSecretValidator.Validar(null!, "segredo-teste"); }
catch (ArgumentNullException) { rejeitouRequestNulo = true; }
Check(rejeitouRequestNulo, "Validador mantém a verificação de HttpRequest nulo");

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
var gmailIncomplete = new GmailInboxOptions
{
    Enabled = true,
    MailboxEmail = "callmed.atendimento@gmail.com",
    ClientId = "client.apps.googleusercontent.com",
    ClientSecret = "client-secret",
    RefreshToken = "refresh-token",
    StartAfterUtc = ""
};
Check(!gmailIncomplete.Configurado, "Gmail API não fica ativo sem instante UTC inicial, evitando importar e-mails antigos");
gmailIncomplete.StartAfterUtc = "2026-10-28T00:00:00Z";
Check(gmailIncomplete.Configurado, "Gmail API aceita caixa, OAuth e instante UTC inicial válidos");

var smtpConfigured = Options.Create(new SmtpOptions
{
    Host = "smtp-relay.brevo.com", Port = 2525, Username = "smtp-user", Password = "smtp-password",
    FromEmail = "nao-responda@example.com", FromName = "CallMed", EnableSsl = true
});
var smtpService = new SmtpEmailService(smtpConfigured, NullLogger<SmtpEmailService>.Instance);
Check(smtpService.Configurado, "Brevo SMTP reporta configurado quando host, porta, usuário, senha e remetente estão preenchidos");
var smtpSemSenha = new SmtpEmailService(Options.Create(new SmtpOptions
{
    Host = "smtp-relay.brevo.com", Port = 2525, Username = "smtp-user", Password = "",
    FromEmail = "nao-responda@example.com", EnableSsl = true
}), NullLogger<SmtpEmailService>.Instance);
Check(!smtpSemSenha.Configurado, "Brevo SMTP não aparece configurado sem senha SMTP");
var smtpSemRemetente = new SmtpEmailService(Options.Create(new SmtpOptions
{
    Host = "smtp-relay.brevo.com", Port = 2525, Username = "smtp-user", Password = "smtp-password",
    FromEmail = "", EnableSsl = true
}), NullLogger<SmtpEmailService>.Instance);
Check(!smtpSemRemetente.Configurado, "Brevo SMTP não aparece configurado sem remetente autorizado definido");


var options = Options.Create(new EvolutionWhatsAppOptions { Enabled = true, BaseUrl = "https://evolution.test", ApiKey = "test-only", InstanceName = "callmed" });
var handler = new FakeHandler();
var sender = new EvolutionWhatsAppSender(new HttpClient(handler), options, NullLogger<EvolutionWhatsAppSender>.Instance, new EvolutionEnvioControle(TimeProvider.System, options));
handler.Body = "{\"instance\":{\"state\":\"open\"}}";
Check((await sender.VerificarConexaoAsync()).Conectado, "Evolution open reconhecido");
Check(handler.LastPath == "/instance/connectionState/callmed" && handler.LastMethod == HttpMethod.Get, "Diagnóstico consulta a instância sem enviar mensagens");
handler.Body = "{\"instance\":{\"state\":\"close\"}}";
Check(!(await sender.VerificarConexaoAsync()).Conectado, "Evolution desconectada não aparece como conectada");
handler.Status = HttpStatusCode.Unauthorized;
Check(!(await sender.VerificarConexaoAsync()).Conectado, "Chave inválida não aparece como conexão ativa");
handler.Status = HttpStatusCode.OK; handler.Body = "invalid";
Check(!(await sender.VerificarConexaoAsync()).Conectado, "Resposta inválida é tratada");
await HistoricoExamesChecks.RunAsync(Check);
await AgendamentoChecks.RunAsync(Check);
SolicitacoesPainelChecks.Run(Check);
await AtendimentoIdentidadeChecks.RunAsync(Check);
await AtendimentoEmailIaChecks.RunAsync(Check);
await AtendimentoRetomadaChecks.RunAsync(Check);
await PortalAcessoChecks.RunAsync(Check);
await EvolutionEnvioChecks.RunAsync(Check);
await EvolutionDiagnosticoChecks.RunAsync(Check);
await HttpSmsChecks.RunAsync(Check);
await FeedbackChecks.RunAsync(Check);
await AgenteLatenciaChecks.RunAsync(Check);
await PasswordResetChecks.RunAsync(Check);
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
