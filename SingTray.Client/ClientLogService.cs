using System.ServiceProcess;
using System.Text;
using SingTray.Shared;

namespace SingTray.Client;

public sealed class ClientLogService : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ClientLogService()
    {
        Directory.CreateDirectory(AppPaths.ClientStateDirectory);
    }

    public Task WriteInfoAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("INFO", message, cancellationToken);

    public Task WriteWarningAsync(string message, CancellationToken cancellationToken = default) =>
        WriteAsync("WARN", message, cancellationToken);

    public Task WriteErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        var detail = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        return WriteAsync("ERROR", detail, cancellationToken);
    }

    public string DescribeServiceState()
    {
        try
        {
            using var controller = new ServiceController(AppPaths.ServiceName);
            return controller.Status switch
            {
                ServiceControllerStatus.Running => "Running",
                ServiceControllerStatus.Stopped => "Stopped",
                ServiceControllerStatus.StartPending => "StartPending",
                ServiceControllerStatus.StopPending => "StopPending",
                ServiceControllerStatus.Paused => "Paused",
                _ => controller.Status.ToString()
            };
        }
        catch (InvalidOperationException)
        {
            return "NotInstalled";
        }
        catch (Exception ex)
        {
            return $"Unknown ({ex.Message})";
        }
    }

    private async Task WriteAsync(string level, string message, CancellationToken cancellationToken)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}";
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(AppPaths.ClientLogPath, line, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
