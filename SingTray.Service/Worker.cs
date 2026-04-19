using SingTray.Service.Services;
using SingTray.Shared;

namespace SingTray.Service;

public sealed class Worker : BackgroundService
{
    private readonly ServiceState _serviceState;
    private readonly LogService _logService;
    private readonly SingBoxManager _singBoxManager;

    public Worker(ServiceState serviceState, LogService logService, SingBoxManager singBoxManager)
    {
        _serviceState = serviceState;
        _logService = logService;
        _singBoxManager = singBoxManager;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        AppPaths.EnsureDataDirectories();
        await _logService.InitializeAsync(cancellationToken);
        await _serviceState.InitializeAsync(cancellationToken);
        await _singBoxManager.CleanupManagedProcessesAsync("service startup cleanup", cancellationToken);
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
        await _singBoxManager.StopForServiceShutdownAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
