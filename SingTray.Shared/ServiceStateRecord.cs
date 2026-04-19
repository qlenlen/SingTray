using SingTray.Shared.Enums;

namespace SingTray.Shared;

public sealed class ServiceStateRecord
{
    public RunState RunState { get; set; } = RunState.Stopped;
    public int? SingBoxPid { get; set; }
    public string? LastError { get; set; }
    public string? CoreVersion { get; set; }
    public string? ConfigName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
