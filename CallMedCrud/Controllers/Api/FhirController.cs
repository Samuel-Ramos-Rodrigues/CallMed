using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Clinica;

namespace MKSANCrud.Controllers;

// Leitura de Patient e Appointment em HL7 FHIR R4 (JSON), para integração com prontuários.
// Autenticação igual à API legada: chave em LegacyIntegration:ApiKey, enviada no header X-CallMed-Integration-Key.
// CPF não é exposto.
[ApiController]
[Route("api/fhir/r4")]
[EnableRateLimiting("webhooks")]
public sealed class FhirController(
    IConfiguration configuration,
    MKSANContext context,
    IClinicaClock clock,
    IDataProtectionProvider protection) : ControllerBase
{
    private readonly IDataProtector _tokens = protection.CreateProtector("CallMed.FhirToken.v1");

    // OAuth2 client credentials (RFC 6749, 4.4). Credenciais em LegacyIntegration:ClientId e ClientSecret.
    // O token vale 1 hora e é protegido pelo DataProtection, sem estado no servidor.
    [HttpPost("oauth/token")]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Token([FromForm] string? grant_type, [FromForm] string? client_id, [FromForm] string? client_secret)
    {
        var clienteEsperado = configuration["LegacyIntegration:ClientId"]?.Trim();
        var segredoEsperado = configuration["LegacyIntegration:ClientSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(clienteEsperado) || string.IsNullOrWhiteSpace(segredoEsperado))
            return Fhir(Erro("not-supported", "OAuth não configurado."), 503);

        if (grant_type != "client_credentials")
            return Fhir(Erro("invalid", "grant_type deve ser client_credentials."), 400);

        if (!ChaveIgual(clienteEsperado, client_id ?? string.Empty) || !ChaveIgual(segredoEsperado, client_secret ?? string.Empty))
            return Fhir(Erro("forbidden", "Credenciais inválidas."), 401);

        var expira = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        return Ok(new { access_token = _tokens.Protect($"fhir|{expira}"), token_type = "Bearer", expires_in = 3600 });
    }

    // Capacidades da API (CapabilityStatement). Não expõe dados de paciente.
    [HttpGet("metadata")]
    public IActionResult Metadata() => Ok(new
    {
        resourceType = "CapabilityStatement",
        status = "active",
        date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
        kind = "instance",
        fhirVersion = "4.0.1",
        format = new[] { "application/fhir+json", "application/json" },
        rest = new[]
        {
            new
            {
                mode = "server",
                resource = new object[]
                {
                    new { type = "Patient", interaction = new[] { new { code = "read" } } },
                    new
                    {
                        type = "Appointment",
                        interaction = new[] { new { code = "read" }, new { code = "search-type" } },
                        searchParam = new[] { new { name = "patient", type = "reference" } }
                    }
                }
            }
        }
    });

    [HttpGet("Patient/{id:int}")]
    public async Task<IActionResult> Patient(int id, CancellationToken ct)
    {
        var auth = Autorizar();
        if (auth is not null) return auth;

        var paciente = await context.Pacientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (paciente is null)
            return Fhir(Erro("not-found", "Paciente não encontrado."), 404);

        var telecom = new List<object>();
        if (!string.IsNullOrWhiteSpace(paciente.Telefone)) telecom.Add(new { system = "phone", value = paciente.Telefone });
        if (!string.IsNullOrWhiteSpace(paciente.Email)) telecom.Add(new { system = "email", value = paciente.Email });

        return Fhir(new
        {
            resourceType = "Patient",
            id = paciente.Id.ToString(CultureInfo.InvariantCulture),
            active = paciente.Ativo,
            name = new[] { new { text = paciente.Nome } },
            telecom
        });
    }

    [HttpGet("Appointment/{id:int}")]
    public async Task<IActionResult> Appointment(int id, CancellationToken ct)
    {
        var auth = Autorizar();
        if (auth is not null) return auth;

        var consulta = await context.Consultas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (consulta is null)
            return Fhir(Erro("not-found", "Consulta não encontrada."), 404);

        return Fhir(RecursoAppointment(consulta));
    }

    // Busca de consultas de um paciente (Appointment?patient={id}), em Bundle searchset. Limite de 100 entradas.
    [HttpGet("Appointment")]
    public async Task<IActionResult> AppointmentSearch([FromQuery] int? patient, CancellationToken ct)
    {
        var auth = Autorizar();
        if (auth is not null) return auth;

        if (patient is null)
            return Fhir(Erro("required", "Informe o parâmetro patient."), 400);

        var consultas = await context.Consultas.AsNoTracking()
            .Where(x => x.PacienteId == patient.Value)
            .OrderBy(x => x.Data).ThenBy(x => x.Horario)
            .Take(100)
            .ToListAsync(ct);

        return Fhir(new
        {
            resourceType = "Bundle",
            type = "searchset",
            total = consultas.Count,
            entry = consultas.Select(c => new
            {
                fullUrl = $"Appointment/{c.Id}",
                resource = RecursoAppointment(c)
            }).ToArray()
        });
    }

    private object RecursoAppointment(Consulta consulta)
    {
        var local = consulta.Data.Date.Add(TimeSpan.TryParse(consulta.Horario, out var hora) ? hora : TimeSpan.Zero);
        var utc = DateTime.SpecifyKind(clock.ConverterParaUtc(local), DateTimeKind.Utc);

        return new
        {
            resourceType = "Appointment",
            id = consulta.Id.ToString(CultureInfo.InvariantCulture),
            status = StatusFhir(consulta.Status),
            start = utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            participant = new object[]
            {
                new { actor = new { reference = $"Patient/{consulta.PacienteId}" }, status = "accepted" },
                new { actor = new { reference = $"Practitioner/{consulta.MedicoId}" }, status = "accepted" }
            }
        };
    }

    private static string StatusFhir(string status) => status switch
    {
        ConsultaStatus.Pendente => "proposed",
        ConsultaStatus.Confirmada or ConsultaStatus.Remarcada => "booked",
        ConsultaStatus.Cancelada => "cancelled",
        ConsultaStatus.Realizada => "fulfilled",
        ConsultaStatus.Ausente => "noshow",
        _ => "pending"
    };

    // Aceita a chave de integração (X-CallMed-Integration-Key) ou um Bearer obtido em oauth/token.
    private IActionResult? Autorizar()
    {
        var chave = configuration["LegacyIntegration:ApiKey"]?.Trim();
        var oauth = configuration["LegacyIntegration:ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(chave) && string.IsNullOrWhiteSpace(oauth))
            return Fhir(Erro("not-supported", "Integração não configurada."), 503);

        var recebida = Request.Headers["X-CallMed-Integration-Key"].FirstOrDefault()?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(chave) && ChaveIgual(chave, recebida))
            return null;

        if (TokenValido(Request.Headers.Authorization.FirstOrDefault()))
            return null;

        return Fhir(Erro("forbidden", "Credenciais de integração inválidas."), 401);
    }

    private static bool ChaveIgual(string esperado, string recebido) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(recebido));

    private bool TokenValido(string? cabecalho)
    {
        const string prefixo = "Bearer ";
        if (string.IsNullOrWhiteSpace(cabecalho) || !cabecalho.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var partes = _tokens.Unprotect(cabecalho[prefixo.Length..].Trim()).Split('|');
            return partes.Length == 2 &&
                   partes[0] == "fhir" &&
                   long.TryParse(partes[1], out var expira) &&
                   DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= expira;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static object Erro(string codigo, string mensagem) => new
    {
        resourceType = "OperationOutcome",
        issue = new[] { new { severity = "error", code = codigo, diagnostics = mensagem } }
    };

    private static IActionResult Fhir(object corpo, int status = 200) => new ContentResult
    {
        Content = JsonSerializer.Serialize(corpo),
        ContentType = "application/fhir+json",
        StatusCode = status
    };
}
