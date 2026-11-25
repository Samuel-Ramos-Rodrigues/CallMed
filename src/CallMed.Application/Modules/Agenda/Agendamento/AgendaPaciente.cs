using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;

namespace MKSANCrud.Services.Agendamento;

internal static class AgendaPaciente
{
    public const string MensagemConflito = "Você já tem outra consulta nesse intervalo. Escolha um horário diferente.";

    public static async Task BloquearDiaAsync(MKSANContext db, int pacienteId, DateTime data, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        var chave = $"paciente-dia:{pacienteId}:{data:yyyy-MM-dd}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({chave}, 0));", ct);
    }

    public static async Task<bool> TemConflitoAsync(MKSANContext db, int pacienteId, DateTime dia,
        string horario, int duracao, int? ignorar, CancellationToken ct)
    {
        var consultas = await db.Consultas.AsNoTracking()
            .Where(c => c.PacienteId == pacienteId && c.Data.Date == dia.Date &&
                c.Status != ConsultaStatus.Cancelada && (!ignorar.HasValue || c.Id != ignorar.Value))
            .Select(c => new { c.Horario, Duracao = db.Jornadas.Where(j => j.ConsultaId == c.Id)
                .Select(j => (int?)j.Duracao).FirstOrDefault() ?? c.DuracaoMinutos })
            .ToListAsync(ct);
        return consultas.Any(c => AgendamentoService.Sobrepoe(horario, duracao, c.Horario, c.Duracao));
    }
}
