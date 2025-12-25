namespace Pico.DI.Test.Decorators;

/// <summary>
/// Service interfaces and implementations for decorator tests
/// </summary>

public interface IUser
{
    string Name { get; }
}

public class User : IUser
{
    public string Name => "John Doe";
}

public interface IEmailService
{
    void SendEmail(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    public void SendEmail(string to, string subject, string body)
    {
        System.Diagnostics.Debug.WriteLine($"Email sent to {to}");
    }
}

public interface IDecoratorLogger
{
    void Log(string message);
}

public class ConsoleDecoratorLogger : IDecoratorLogger
{
    public void Log(string message) => System.Diagnostics.Debug.WriteLine(message);
}

/// <summary>
/// Generic decorator that logs method calls.
/// Can wrap any service type T.
/// </summary>
public class Logger<T>
    where T : class
{
    private readonly T _inner;
    private readonly List<string> _logs = [];

    public Logger(T inner)
    {
        _inner = inner;
        _logs.Add($"Created Logger<{typeof(T).Name}>");
    }

    public T GetInner() => _inner;

    public IReadOnlyList<string> Logs => _logs;

    public void LogAccess()
    {
        _logs.Add($"Accessed {typeof(T).Name}");
    }
}

/// <summary>
/// A more complex decorator with additional dependencies.
/// </summary>
public class CachingDecorator<T>
    where T : class
{
    private readonly T _inner;
    private readonly IDecoratorLogger _logger;
    private readonly Dictionary<string, object> _cache = [];

    public CachingDecorator(T inner, IDecoratorLogger logger)
    {
        _inner = inner;
        _logger = logger;
        _logger.Log($"Created CachingDecorator<{typeof(T).Name}>");
    }

    public T GetInner() => _inner;

    public void CacheValue(string key, object value)
    {
        _cache[key] = value;
        _logger.Log($"Cached {key}");
    }
}
