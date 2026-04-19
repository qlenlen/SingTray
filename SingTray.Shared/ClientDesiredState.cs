namespace SingTray.Shared;

public sealed class ClientDesiredState
{
    public bool ShouldBeRunning { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
