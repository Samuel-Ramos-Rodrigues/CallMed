using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Clinica;

public sealed class TeleconsultaAcessoService(MKSANContext db, JornadaService jornada, IClinicaClock clock)
{
    public async Task<TeleconsultaAcesso?> ObterAsync(int id, ClaimsPrincipal user, CancellationToken ct)
    {
        var consulta = await jornada.Visiveis(user).AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
        if (consulta == null) return null;
        var preparacao = await db.Jornadas.AsNoTracking().SingleOrDefaultAsync(j => j.ConsultaId == id, ct);
        DateTime? inicio = TimeSpan.TryParse(consulta.Horario, CultureInfo.InvariantCulture, out var horario)
            && horario >= TimeSpan.Zero && horario < TimeSpan.FromDays(1)
            ? consulta.Data.Date.Add(horario) : null;
        return new(consulta, preparacao,
            user.IsInRole("Paciente") && JornadaService.Paciente(user, consulta),
            JornadaService.Medico(user, consulta), inicio, inicio?.AddMinutes(-30),
            inicio?.AddHours(4), clock.Agora){Bloqueio=await new PsicologiaService(db,clock).ImpedimentoAsync(consulta,ct)};
    }
}
