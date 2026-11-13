using MKSANCrud.Models;
namespace MKSANCrud.Services.Clinica;

public static class FhirPractitioner
{
    public static object Criar(Psicologo profissional, string? sistema = null)
    {
        var ids = new List<object>();
        if (Uri.TryCreate(sistema, UriKind.Absolute, out _))
            ids.Add(new { system = sistema + ":Practitioner", value = profissional.Id.ToString() });
        if (profissional.EhPsicologia && !string.IsNullOrWhiteSpace(profissional.Crp))
            ids.Add(new { system = "urn:callmed:crp", value = profissional.Crp });
        else if (!string.IsNullOrWhiteSpace(profissional.RegistroNumero) && !string.IsNullOrWhiteSpace(profissional.RegistroConselho))
            ids.Add(new { system = "urn:callmed:registro:" + profissional.RegistroConselho + ":" + profissional.RegistroUf, value = profissional.RegistroNumero });
        return new { resourceType = "Practitioner", id = profissional.Id.ToString(),
            active = profissional.HabilitadoClinicamente, name = new[] { new { text = profissional.Nome } }, identifier = ids };
    }
}
