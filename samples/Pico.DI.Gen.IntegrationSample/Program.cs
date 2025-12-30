// ConfigureGeneratedServices extension is generated into this namespace

namespace Pico.DI.Gen.IntegrationSample;

public interface ILog<T> { }

public class ConsoleLog<T> : ILog<T> { }

public class UserService
{
    public ILog<UserService> Log { get; }

    public UserService(ILog<UserService> log) => Log = log;
}

internal class Program
{
    static void Main()
    {
        using var container = new SvcContainer();

        // Register open generic logger as descriptor for generator to use
        container.RegisterTransient(typeof(ILog<>), typeof(ConsoleLog<>));

        // Register the concrete service via factory which depends on the closed generic logger
        container.RegisterTransient<UserService>(scope => new UserService(
            scope.GetService<ILog<UserService>>()
        ));

        // Configure generated services (the generated method should exist after build)
        container.ConfigureGeneratedServices();

        using var scope = container.CreateScope();
        var log = scope.GetService<ILog<UserService>>();

        Console.WriteLine(
            log is null ? "ILog<UserService> not resolved" : "ILog<UserService> resolved"
        );

        var userSvc = scope.GetService<UserService>();
        Console.WriteLine(userSvc is null ? "UserService not resolved" : "UserService resolved");
    }
}
