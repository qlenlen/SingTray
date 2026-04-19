namespace SingTray.Shared.Models;

public sealed class CoreInfo
{
    public bool Installed { get; set; }
    public bool Valid { get; set; }
    public string? Version { get; set; }
    public string? ValidationMessage { get; set; }
}
