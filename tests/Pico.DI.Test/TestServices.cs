namespace Pico.DI.Test;

#region Basic Service Interfaces and Implementations

public interface ISimpleService
{
    Guid InstanceId { get; }
}

public class SimpleService : ISimpleService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface IServiceWithDependency
{
    ISimpleService Dependency { get; }
    Guid InstanceId { get; }
}

public class ServiceWithDependency(ISimpleService dependency) : IServiceWithDependency
{
    public ISimpleService Dependency { get; } = dependency;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion

#region Disposable Services

public interface IDisposableService : IDisposable
{
    Guid InstanceId { get; }
    bool IsDisposed { get; }
}

public class DisposableService : IDisposableService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

public interface IAsyncDisposableService : IAsyncDisposable
{
    Guid InstanceId { get; }
    bool IsDisposed { get; }
}

public class AsyncDisposableService : IAsyncDisposableService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

#endregion

#region Open Generic Services

public interface IRepository<T>
{
    Guid InstanceId { get; }
    Type EntityType { get; }
}

public class Repository<T> : IRepository<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public Type EntityType => typeof(T);
}

public interface ILogger<T>
{
    Guid InstanceId { get; }
    Type CategoryType { get; }
}

public class Logger<T> : ILogger<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public Type CategoryType => typeof(T);
}

// Entity types for open generic tests
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}

#endregion

#region Multiple Implementation Services

public interface INotificationService
{
    string NotificationType { get; }
    Guid InstanceId { get; }
}

public class EmailNotificationService : INotificationService
{
    public string NotificationType => "Email";
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public class SmsNotificationService : INotificationService
{
    public string NotificationType => "SMS";
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public class PushNotificationService : INotificationService
{
    public string NotificationType => "Push";
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion

#region Complex Dependency Chain Services

public interface ILevelOneService
{
    Guid InstanceId { get; }
}

public class LevelOneService : ILevelOneService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface ILevelTwoService
{
    ILevelOneService LevelOne { get; }
    Guid InstanceId { get; }
}

public class LevelTwoService(ILevelOneService levelOne) : ILevelTwoService
{
    public ILevelOneService LevelOne { get; } = levelOne;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface ILevelThreeService
{
    ILevelTwoService LevelTwo { get; }
    Guid InstanceId { get; }
}

public class LevelThreeService(ILevelTwoService levelTwo) : ILevelThreeService
{
    public ILevelTwoService LevelTwo { get; } = levelTwo;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion

#region Services for Factory Tests

public interface IConfigurableService
{
    string Configuration { get; }
    Guid InstanceId { get; }
}

public class ConfigurableService(string config) : IConfigurableService
{
    public string Configuration { get; } = config;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion
