using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MKSANCrud.Data;
using MKSANCrud.Models;
using MKSANCrud.Services.Usuarios;

namespace MKSANCrud.Services.Clinica;

public sealed class ConsultaLinkService(MKSANContext db, IDataProtectionProvider dp, IConfiguration config)
{
    public async Task<string?> CriarAsync(Consulta c, CancellationToken ct)
    {
        // Uma URL pública única é obrigatória para links criados tanto pelo worker quanto pelo web.
        // RENDER_EXTERNAL_URL pode pertencer ao worker, por isso não define o destino do paciente.
        var origin = PublicOrigin.TentarObter(config);
        if (origin is null) return null;

        var snapshot = JornadaService.Snapshot(c);
        var link = await db.LinksConsulta.FirstOrDefaultAsync(x =>
            x.ConsultaId == c.Id && x.Snapshot == snapshot && x.UsadoEm == null &&
            x.ExpiraEm > DateTime.UtcNow.AddHours(2), ct);
        if (link is null)
        {
            link = new LinkConsulta { ConsultaId = c.Id, Snapshot = snapshot, ExpiraEm = DateTime.UtcNow.AddHours(48) };
            db.LinksConsulta.Add(link);
            await db.SaveChangesAsync(ct);
        }

        // Preserva o formato dos tokens protegidos dos links já enviados.
        var token = dp.CreateProtector("CallMed.ConsultaLink.v1").Protect(link.Id.ToString());
        return new Uri(origin, "consulta-acao?token=" + Uri.EscapeDataString(token)).AbsoluteUri;
    }

    public Guid? Ler(string? token)
    {
        try
        {
            return Guid.Parse(dp.CreateProtector("CallMed.ConsultaLink.v1").Unprotect(token ?? ""));
        }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
