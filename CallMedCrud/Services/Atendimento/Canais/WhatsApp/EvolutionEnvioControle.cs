using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MKSANCrud.Options;

namespace MKSANCrud.Services.Atendimento.Canais.WhatsApp;

// Compartilhado pelos clientes HTTP, workers e requisições desta aplicação.
public sealed class EvolutionEnvioControle(TimeProvider clock, IOptions<EvolutionWhatsAppOptions> options)
{
    internal SemaphoreSlim Fila { get; } = new(1, 1);
    private DateTimeOffset _ultimoEnvio = DateTimeOffset.MinValue;
    private DateTimeOffset _bloqueadoAte = DateTimeOffset.MinValue;
    private int _limitesConsecutivos;

    internal DateTime? BloqueadoAte => _bloqueadoAte > clock.GetUtcNow() ? _bloqueadoAte.UtcDateTime : null;

    internal async Task AguardarIntervaloAsync(CancellationToken ct)
    {
        var intervalo = TimeSpan.FromMilliseconds(Math.Clamp(options.Value.IntervaloMinimoEnvioMs, 100, 60000));
        var restante = _ultimoEnvio + intervalo - clock.GetUtcNow();
        if (restante > TimeSpan.Zero) await Task.Delay(restante, clock, ct);
        _ultimoEnvio = clock.GetUtcNow();
    }

    internal DateTime RegistrarLimite(RetryConditionHeaderValue? retryAfter)
    {
        var agora = clock.GetUtcNow();
        var espera = retryAfter?.Delta ?? (retryAfter?.Date - agora);
        if (espera is null || espera <= TimeSpan.Zero)
            espera = TimeSpan.FromSeconds(Math.Clamp(options.Value.EsperaApos429Segundos, 1, 86400)
                * Math.Pow(2, Math.Min(_limitesConsecutivos, 4)));
        _limitesConsecutivos++;
        var limite = agora + espera.Value;
        if (limite > _bloqueadoAte) _bloqueadoAte = limite;
        return _bloqueadoAte.UtcDateTime;
    }

    internal void RegistrarSucesso() => _limitesConsecutivos = 0;
}
