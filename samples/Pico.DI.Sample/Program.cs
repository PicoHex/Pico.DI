namespace Pico.DI.Sample;

public static class ServiceConfig
{
    public static void ConfigureServices(ISvcContainer container)
    {
        // Note: ConfigureGeneratedServices() is now called automatically via Module Initializer
        // when SvcContainer is created. Manual registration calls below are for descriptive purposes.
        container
            .RegisterSingleton(typeof(ILogger<>), typeof(ConsoleLogger<>))
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>();
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
