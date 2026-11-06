using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
public sealed class FhirController(IConfiguration configuration, MKSANContext context, IClinicaClock clock) : ControllerBase
{
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

        var local = consulta.Data.Date.Add(TimeSpan.TryParse(consulta.Horario, out var hora) ? hora : TimeSpan.Zero);
        var utc = DateTime.SpecifyKind(clock.ConverterParaUtc(local), DateTimeKind.Utc);

        return Fhir(new
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
        });
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

    private IActionResult? Autorizar()
    {
        var esperado = configuration["LegacyIntegration:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(esperado))
            return Fhir(Erro("not-supported", "Integração não configurada."), 503);

        var recebido = Request.Headers["X-CallMed-Integration-Key"].FirstOrDefault()?.Trim() ?? string.Empty;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(recebido)))
            return Fhir(Erro("forbidden", "Chave de integração inválida."), 401);

        return null;
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
