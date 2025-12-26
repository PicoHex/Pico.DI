namespace Pico.DI.Benchmarks;

// Test services for benchmarking
public interface ITransientService
{
    int GetValue();
}

public class TransientService : ITransientService
{
    public int GetValue() => 42;
}

public interface IScopedService
{
    Guid Id { get; }
}

public class ScopedService : IScopedService
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface ISingletonService
{
    string Name { get; }
}

public class SingletonService : ISingletonService
{
    public string Name => "Singleton";
}

public interface IComplexService
{
    void DoWork();
}

public class ComplexService(
    ITransientService transient,
    IScopedService scoped,
    ISingletonService singleton
) : IComplexService
{
    public void DoWork()
    {
        _ = transient.GetValue();
        _ = scoped.Id;
        _ = singleton.Name;
    }
}

// Deep dependency chain for stress testing
public interface ILevel1 { }

public interface ILevel2 { }

public interface ILevel3 { }

public interface ILevel4 { }

public interface ILevel5 { }

public class Level1 : ILevel1 { }

public class Level2(ILevel1 l1) : ILevel2 { }

public class Level3(ILevel2 l2) : ILevel3 { }

public class Level4(ILevel3 l3) : ILevel4 { }

public class Level5(ILevel4 l4) : ILevel5 { }
