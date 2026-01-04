// Service registrations are now auto-configured via Module Initializer

namespace Pico.DI.Gen.IntegrationSample;

public interface ILog<T>;

public class ConsoleLog<T> : ILog<T>;

public class UserService(ILog<UserService> log)
{
    public ILog<UserService> Log { get; } = log;
}

internal static class Program
{
    static void Main()
    {
        // SvcContainer constructor automatically applies source-generated service registrations
        // via Module Initializer - no manual ConfigureGeneratedServices() call needed!
        using var container = new SvcContainer();

        // Register open generic logger as descriptor for generator to use
        container.RegisterTransient(typeof(ILog<>), typeof(ConsoleLog<>));

        // Register the concrete service via factory which depends on the closed generic logger
        container.RegisterTransient<UserService>(
            scope => new UserService(scope.GetService<ILog<UserService>>())
        );

        using var scope = container.CreateScope();
        var log = scope.GetService<ILog<UserService>>();

        Console.WriteLine(
            log is null ? "ILog<UserService> not resolved" : "ILog<UserService> resolved"
        );

        var userSvc = scope.GetService<UserService>();
        Console.WriteLine(userSvc is null ? "UserService not resolved" : "UserService resolved");
    }
}
