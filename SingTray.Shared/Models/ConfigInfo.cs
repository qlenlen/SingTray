namespace SingTray.Shared.Models;

public sealed class ConfigInfo
{
    public bool Installed { get; set; }
    public bool Valid { get; set; }
    public string? FileName { get; set; }
    public string? ValidationMessage { get; set; }
}
