namespace Pico.IoC.Test;

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
}
