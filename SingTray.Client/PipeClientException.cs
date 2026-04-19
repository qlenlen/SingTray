namespace SingTray.Client;

public enum PipeFailureKind
{
    Unknown = 0,
    ServiceNotRunning = 1,
    PipeNotFound = 2,
    Timeout = 3,
    AccessDenied = 4,
    InvalidResponse = 5,
    ServiceError = 6
}

public sealed class PipeClientException : Exception
{
    public PipeClientException(PipeFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public PipeFailureKind Kind { get; }
}
