using System.Text.Json;
using SingTray.Shared;

namespace SingTray.Client;

public sealed class DesiredStateStore : IDisposable
{
    public async Task<bool> ReadAsync()
    {
        Directory.CreateDirectory(AppPaths.ClientStateDirectory);
        if (!File.Exists(AppPaths.ClientDesiredStatePath))
        {
            return false;
        }

        await using var stream = File.OpenRead(AppPaths.ClientDesiredStatePath);
        var state = await JsonSerializer.DeserializeAsync<ClientDesiredState>(stream, PipeContracts.JsonOptions);
        return state?.ShouldBeRunning ?? false;
    }

    public async Task WriteAsync(bool shouldBeRunning)
    {
        Directory.CreateDirectory(AppPaths.ClientStateDirectory);
        await using var stream = File.Create(AppPaths.ClientDesiredStatePath);
        await JsonSerializer.SerializeAsync(
            stream,
            new ClientDesiredState { ShouldBeRunning = shouldBeRunning, UpdatedAt = DateTimeOffset.UtcNow },
            PipeContracts.JsonOptions);
    }

    public void Dispose()
    {
    }
}
