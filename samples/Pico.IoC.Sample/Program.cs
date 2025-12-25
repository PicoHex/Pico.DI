namespace Pico.IoC.Sample;

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
