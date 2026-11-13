using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Models.Atendimento;
using MKSANCrud.Services.Atendimento;

namespace MKSANCrud.Services.Usuarios;
public static class ContaAvisos
{
    public static async Task<(CanalAtendimento Canal, string Identificador)?> EscolherAsync(MKSANContext db, Paciente paciente, AtendimentoEnvioService envio, CancellationToken ct)
    {
        var prefs = paciente.UsuarioId is null ? null : await db.ContaPerfis.AsNoTracking().Where(p => p.UsuarioId == paciente.UsuarioId)
            .Select(p => new { p.AvisosWhatsApp, p.AvisosEmail, p.AvisosSms }).FirstOrDefaultAsync(ct);
        var ordem = new[] { paciente.CanalPreferido, "WhatsApp", "SMS", "Email" }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var nome in ordem)
        {
            var canal = nome?.ToLowerInvariant() switch { "whatsapp" => CanalAtendimento.WhatsApp, "sms" => CanalAtendimento.Sms, "email" or "e-mail" => CanalAtendimento.Email, _ => (CanalAtendimento?)null };
            if (canal is null) continue;
            var permitido = canal == CanalAtendimento.WhatsApp ? prefs?.AvisosWhatsApp ?? true : canal == CanalAtendimento.Sms ? prefs?.AvisosSms ?? true : prefs?.AvisosEmail ?? true;
            var destino = canal == CanalAtendimento.Email ? paciente.Email : paciente.Telefone;
            if (permitido && !string.IsNullOrWhiteSpace(destino) && envio.CanalConfigurado(canal.Value)) return (canal.Value, destino);
        }
        return null;
    }
}
