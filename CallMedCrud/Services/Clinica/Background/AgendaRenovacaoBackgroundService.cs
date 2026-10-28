namespace MKSANCrud.Services.Clinica;

/// <summary>
/// Renova diariamente a janela rolante de disponibilidades geradas pelas agendas semanais.
/// Assim, um médico não fica sem vagas depois de 120 dias sem ser editado.
/// </summary>
public sealed class AgendaRenovacaoBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgendaRenovacaoBackgroundService> _logger;

    public AgendaRenovacaoBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgendaRenovacaoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RenovarAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RenovarAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal da aplicação.
        }
    }

    private async Task RenovarAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaMedicoService>();
            await agenda.SincronizarTodosMedicosAtivosAsync(120, ct);
            _logger.LogInformation("Janela de agenda médica renovada com sucesso.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao renovar a janela de agenda médica.");
        }
    }
}
