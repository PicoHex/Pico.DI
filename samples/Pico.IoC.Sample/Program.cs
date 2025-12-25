using Pico.IoC;
using Pico.IoC.Abs;
using Pico.IoC.Gen;

namespace Pico.IoC.Sample;

// Example services
public interface IGreeter
{
    string Greet(string name);
}

public class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}!";
}

public interface ILogger
{
    void Log(string message);
}

public class ConsoleLogger : ILogger
{
    public void Log(string message) => Console.WriteLine($"[LOG] {message}");
}

public class GreetingService(IGreeter greeter, ILogger logger)
{
    public void SayHello(string name)
    {
        logger.Log($"Greeting {name}");
        Console.WriteLine(greeter.Greet(name));
    }
}

/// <summary>
/// Service registration configuration.
/// </summary>
public static class ServiceConfig
{
    public static void ConfigureServices(ISvcContainer container)
    {
        // These Register* calls are scanned by Source Generator at compile time.
        // They don't actually register anything - the generated ConfigureGeneratedServices() does.
        container
            .RegisterSingleton<ILogger, ConsoleLogger>()
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>();

        // Apply the auto-generated factory-based registrations
        container.ConfigureGeneratedServices();
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        using var container = new SvcContainer();

        // Configure services
        ServiceConfig.ConfigureServices(container);

        // Create a scope and resolve services
        using var scope = container.CreateScope();

        var greetingService = scope.GetService<GreetingService>();
        greetingService.SayHello("World");
    }
}
