namespace MKSANCrud.Services.Clinica;

public sealed class ListaEsperaBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ListaEsperaBackgroundService> _logger;
    public ListaEsperaBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ListaEsperaBackgroundService> logger) { _scopeFactory = scopeFactory; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try { using var scope = _scopeFactory.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<ListaEsperaService>(); var n = await service.ProcessarNotificacoesAsync(stoppingToken); await OperacaoService.RegistrarAsync(scope.ServiceProvider,"espera",true,stoppingToken); if (n > 0) _logger.LogInformation("Lista de espera notificou {Quantidade} paciente(s).", n); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Falha ao processar lista de espera."); using var scope=_scopeFactory.CreateScope();await OperacaoService.RegistrarAsync(scope.ServiceProvider,"espera",false,stoppingToken); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
