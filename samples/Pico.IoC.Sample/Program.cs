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
        // Register services - the Source Generator will scan these calls
        // and generate factory methods automatically
        container
            .RegisterSingleton<ILogger, ConsoleLogger>()
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>();

        // Register the generated descriptors with pre-compiled factories
        // This replaces the type-based registrations above with AOT-compatible factories
        container.RegisterRange(GeneratedServiceRegistrations.GetDescriptors());
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
