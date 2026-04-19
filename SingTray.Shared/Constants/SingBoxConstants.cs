namespace SingTray.Shared.Constants;

public static class SingBoxConstants
{
    public static readonly string[] RunArguments = ["run", "-c", AppPaths.ActiveConfigPath];
    public static readonly string[] CheckArguments = ["check", "-c"];
    public static readonly string[] VersionArguments = ["version"];
    public const int StartProbeDelayMilliseconds = 1200;
    public const int StopTimeoutMilliseconds = 8000;
    public const int PipeTimeoutMilliseconds = 8000;
    public const int PipeImportTimeoutMilliseconds = 45000;
}
