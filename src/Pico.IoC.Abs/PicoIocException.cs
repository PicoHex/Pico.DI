namespace Pico.IoC.Abs;

/// <summary>
/// Exception thrown by Pico.IoC container operations.
/// </summary>
public class PicoIocException : Exception
{
    public PicoIocException() { }

    public PicoIocException(string message)
        : base(message) { }

    public PicoIocException(string message, Exception innerException)
        : base(message, innerException) { }
}
