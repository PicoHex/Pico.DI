namespace Pico.DI.Sample;

public static class Program
{
    public static async Task Main(string[] args)
    {
        await using var container = new SvcContainer()
            .RegisterSingleton(typeof(ILogger<>), typeof(ConsoleLogger<>))
            .RegisterTransient<IGreeter, Greeter>()
            .RegisterScoped<GreetingService>();

        // Create a scope and resolve services
        await using var scope = container.CreateScope();

        var greetingService = scope.GetService<GreetingService>();
        greetingService.SayHello("World");
    }
}
