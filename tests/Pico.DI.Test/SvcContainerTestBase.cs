namespace Pico.DI.Test;

/// <summary>
/// Test base class containing shared test services and utilities.
/// </summary>
public abstract class SvcContainerTestBase
{
    #region Test Services

    public interface IGreeter
    {
        string Greet(string name);
    }

    public class ConsoleGreeter : IGreeter
    {
        public string Greet(string name) => $"Hello, {name}!";
    }

    public class AlternativeGreeter : IGreeter
    {
        public string Greet(string name) => $"Hi, {name}!";
    }

    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message) => Console.WriteLine($"[LOG] {message}");
    }

    #endregion

    // Test helpers for reducing duplicated registration code
    protected void RegisterConsoleGreeter(
        ISvcContainer container,
        SvcLifetime lifetime = SvcLifetime.Transient
    )
    {
        switch (lifetime)
        {
            case SvcLifetime.Singleton:
                container.RegisterSingleton<IGreeter>(_ => new ConsoleGreeter());
                break;
            case SvcLifetime.Scoped:
                container.RegisterScoped<IGreeter>(_ => new ConsoleGreeter());
                break;
            default:
                container.RegisterTransient<IGreeter>(_ => new ConsoleGreeter());
                break;
        }
    }

    protected void RegisterAlternativeGreeter(
        ISvcContainer container,
        SvcLifetime lifetime = SvcLifetime.Transient
    )
    {
        switch (lifetime)
        {
            case SvcLifetime.Singleton:
                container.RegisterSingleton<IGreeter>(_ => new AlternativeGreeter());
                break;
            case SvcLifetime.Scoped:
                container.RegisterScoped<IGreeter>(_ => new AlternativeGreeter());
                break;
            default:
                container.RegisterTransient<IGreeter>(_ => new AlternativeGreeter());
                break;
        }
    }

    protected void RegisterGreeterPair(ISvcContainer container, SvcLifetime lifetime)
    {
        RegisterConsoleGreeter(container, lifetime);
        RegisterAlternativeGreeter(container, lifetime);
    }
}
