using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Options;
using MKSANCrud.Services.Clinica;
using MKSANCrud.Services.Atendimento.Canais.Email;
using MKSANCrud.Services.Email;
using MKSANCrud.Services.Psicologia;
using MKSANCrud.Services.Usuarios;

internal static class LembretesLinkChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicOrigin"] = "https://callmed.onrender.com",
            ["RENDER_EXTERNAL_URL"] = "https://callmed-worker.onrender.com",
            ["Twa:Origin"] = "https://callmed-antigo.invalid"
        }).Build();
        check(PublicOrigin.TentarObter(config)?.Host == "callmed.onrender.com",
            "Links enviados usam PublicOrigin em vez do domínio do worker ou TWA");

        await using var db = new MKSANContext(new DbContextOptionsBuilder<MKSANContext>()
            .UseInMemoryDatabase("lembretes-links-" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var c = new Consulta { Id = 21001, PacienteId = 1, MedicoId = 1,
            Data = DateTime.Today.AddDays(2), Horario = "11:00" };
        db.Consultas.Add(c);
        await db.SaveChangesAsync();
        var dp = new EphemeralDataProtectionProvider();
        var service = new ConsultaLinkService(db, dp, config);
        var url = await service.CriarAsync(c, default);
        check(url is not null && url.StartsWith("https://callmed.onrender.com/consulta-acao?token=", StringComparison.Ordinal),
            "Lembrete aponta para a rota pública segura de confirmação");
        var token = Uri.UnescapeDataString(new Uri(url!).Query["?token=".Length..]);
        check(service.Ler(token) is Guid id && id == (await db.LinksConsulta.SingleAsync()).Id,
            "Link mantém token criptografado compatível com DataProtection");
        check(service.Ler(token + "invalido") is null,
            "Token adulterado não é aceito");

        var fake = new EmailFake();
        var sender = new SmtpAtendimentoSender(fake,
            Options.Create(new AtendimentoEmailOptions { PortalBaseUrl = "https://callmed.onrender.com" }), config);
        await sender.EnviarAsync("paciente@example.invalid",
            "Lembrete CallMed: confira sua consulta.\n" + url + "\nNão responda para cancelar.");
        check(fake.Html.Contains("<a href=\"" + url + "\"", StringComparison.Ordinal) &&
              fake.Html.Contains("/consulta-acao?token=", StringComparison.Ordinal),
            "E-mail do lembrete torna a URL de consulta um link clicável");
        await sender.EnviarAsync("paciente@example.invalid", "<script>teste</script> " + url);
        check(!fake.Html.Contains("<script>", StringComparison.Ordinal) &&
               fake.Html.Contains("&lt;script&gt;", StringComparison.Ordinal),
            "E-mail mantém escape HTML em texto com URL");

        var prep = new PreparacaoLinkService(dp, config).Criar(c);
        check(prep?.StartsWith("https://callmed.onrender.com/preparacao?token=", StringComparison.Ordinal) == true,
            "Link de preparação também aponta para o domínio público correto");

        var legacy = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["RENDER_EXTERNAL_URL"] = "https://callmed-antigo.example" }).Build();
        check(PublicOrigin.TentarObter(legacy)?.Host == "callmed-antigo.example",
            "Instalações antigas sem PublicOrigin mantêm origem HTTPS de fallback");
        var invalid = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["PublicOrigin"] = "https://callmed.onrender.com/caminho", ["RENDER_EXTERNAL_URL"] = "https://worker.invalid" }).Build();
        check(PublicOrigin.TentarObter(invalid) is null,
            "Origem pública explicitamente inválida não produz link em outro domínio");
    }

    private sealed class EmailFake : IEmailService
    {
        public bool Configurado => true;
        public string Html { get; private set; } = "";
        public Task<bool> EnviarAsync(string destinatario, string assunto, string corpoHtml,
            CancellationToken ct = default, string? replyTo = null)
        {
            Html = corpoHtml;
            return Task.FromResult(true);
        }
    }
}
