using SingTray.Service.Services;
using SingTray.Shared;

namespace SingTray.Service;

public sealed class Worker : BackgroundService
{
    private readonly ServiceState _serviceState;
    private readonly LogService _logService;

    public Worker(ServiceState serviceState, LogService logService)
    {
        _serviceState = serviceState;
        _logService = logService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        AppPaths.EnsureDataDirectories();
        await _logService.InitializeAsync(cancellationToken);
        await _serviceState.InitializeAsync(cancellationToken);
        await _logService.WriteInfoAsync("SingTray service started.", cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _logService.WriteInfoAsync("SingTray service stopping.", cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
