namespace Memshare;

/// <summary>
/// An error the user can act on. Its message is shown as is, without a stack trace.
/// </summary>
public sealed class MemshareException : Exception
{
    public MemshareException()
    {
    }

    public MemshareException(string message)
        : base(message)
    {
    }

    public MemshareException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
