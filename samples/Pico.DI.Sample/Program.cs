namespace Pico.DI.Sample;

public static class ServiceConfig
{
    public static void ConfigureServices(ISvcContainer container)
    {
        container
            .RegisterSingleton<ILogger, ConsoleLogger>()
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>()
            .ConfigureGeneratedServices();
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
