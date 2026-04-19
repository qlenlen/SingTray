using SingTray.Shared.Models;

namespace SingTray.Client;

public sealed class StatusPoller : IDisposable
{
    private readonly PipeClient _pipeClient;
    private readonly System.Windows.Forms.Timer _timer;
    private int _isPolling;

    public event EventHandler<StatusInfo>? StatusUpdated;
    public event EventHandler<Exception>? PollFailed;

    public StatusPoller(PipeClient pipeClient)
    {
        _pipeClient = pipeClient;
        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public async Task PollNowAsync() => await PollAsync();

    private async Task PollAsync()
    {
        if (Interlocked.Exchange(ref _isPolling, 1) == 1)
        {
            return;
        }

        try
        {
            var status = await _pipeClient.GetStatusAsync(CancellationToken.None);
            StatusUpdated?.Invoke(this, status);
        }
        catch (Exception ex)
        {
            PollFailed?.Invoke(this, ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
