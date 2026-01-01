namespace Pico.DI.Test;

/// <summary>
/// Tests for error handling and edge cases.
/// </summary>
public class SvcContainerErrorTests : SvcContainerTestBase
{
    #region PicoDiException Tests

    [Fact]
    public void PicoDiException_DefaultConstructor()
    {
        // Act
        var ex = new PicoDiException();

        // Assert
        Assert.NotNull(ex);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void PicoDiException_MessageConstructor()
    {
        // Act
        var ex = new PicoDiException("Test message");

        // Assert
        Assert.Equal("Test message", ex.Message);
    }

    [Fact]
    public void PicoDiException_MessageAndInnerExceptionConstructor()
    {
        // Arrange
        var innerEx = new InvalidOperationException("Inner");

        // Act
        var ex = new PicoDiException("Outer", innerEx);

        // Assert
        Assert.Equal("Outer", ex.Message);
        Assert.Same(innerEx, ex.InnerException);
    }

    #endregion

    [Fact]
    public void GetService_UnregisteredType_ThrowsPicoDiException()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert
        using var scope = container.CreateScope();
        var ex = Assert.Throws<PicoDiException>(() => scope.GetService<IGreeter>());
        Assert.Contains("IGreeter", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void GetServices_UnregisteredType_ThrowsPicoDiException()
    {
        // Arrange
        var container = new SvcContainer();

        // Act & Assert
        using var scope = container.CreateScope();
        var ex = Assert.Throws<PicoDiException>(() => scope.GetServices<IGreeter>().ToList());
        Assert.Contains("IGreeter", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void SvcDescriptor_NullServiceType_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(null!, typeof(ConsoleGreeter))
        );
    }

    [Fact]
    public void SvcDescriptor_NullInstance_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(typeof(IGreeter), (object)null!)
        );
    }

    [Fact]
    public void SvcDescriptor_NullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new SvcDescriptor(typeof(IGreeter), (Func<ISvcScope, object>)null!)
        );
    }

    [Fact]
    public void SvcDescriptor_WithTypeOnly_SetsImplementationTypeToServiceType()
    {
        // Act
        var descriptor = new SvcDescriptor(typeof(ConsoleGreeter), (Type?)null);

        // Assert
        Assert.Equal(typeof(ConsoleGreeter), descriptor.ServiceType);
        Assert.Equal(typeof(ConsoleGreeter), descriptor.ImplementationType);
    }

    [Fact]
    public void SvcDescriptor_WithInstance_SetsProperties()
    {
        // Arrange
        var instance = new ConsoleGreeter();

        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), instance);

        // Assert
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.Same(instance, descriptor.SingleInstance);
        Assert.Null(descriptor.Factory);
    }

    [Fact]
    public void SvcDescriptor_WithFactory_SetsProperties()
    {
        // Act
        var descriptor = new SvcDescriptor(
            typeof(IGreeter),
            _ => new ConsoleGreeter(),
            SvcLifetime.Transient
        );

        // Assert
        Assert.Equal(typeof(IGreeter), descriptor.ServiceType);
        Assert.NotNull(descriptor.Factory);
        Assert.Null(descriptor.SingleInstance);
        Assert.Equal(SvcLifetime.Transient, descriptor.Lifetime);
    }

    [Fact]
    public void SvcDescriptor_DefaultLifetime_IsSingleton()
    {
        // Act
        var descriptor = new SvcDescriptor(typeof(IGreeter), typeof(ConsoleGreeter));

        // Assert
        Assert.Equal(SvcLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void Container_Register_SameServiceMultipleTimes_AddsAll()
    {
        // Arrange
        var container = new SvcContainer();

        // Act
        RegisterGreeterPair(container, SvcLifetime.Singleton);

        // Assert
        using var scope = container.CreateScope();
        var services = scope.GetServices<IGreeter>().ToList();
        Assert.Equal(2, services.Count);
    }

    [Fact]
    public void Container_Register_ReturnsContainerForChaining()
    {
        // Arrange
        var container = new SvcContainer();
        var descriptor = new SvcDescriptor(typeof(IGreeter), _ => new ConsoleGreeter());

        // Act
        var result = container.Register(descriptor);

        // Assert
        Assert.Same(container, result);
    }

    [Fact]
    public async Task Scope_Singleton_ThreadSafe_MultipleThreadsGetSameInstance()
    {
        // Arrange
        var container = new SvcContainer();
        var instanceCount = 0;
        container.RegisterSingleton<IGreeter>(_ =>
        {
            Interlocked.Increment(ref instanceCount);
            Thread.Sleep(10); // Simulate slow instantiation
            return new ConsoleGreeter();
        });

        using var scope = container.CreateScope();

        // Act - Multiple threads trying to get singleton
        var tasks = Enumerable
            .Range(0, 10)
            .Select(_ => Task.Run(() => scope.GetService<IGreeter>()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - Should only create one instance
        Assert.Equal(1, instanceCount);
        Assert.All(results, r => Assert.Same(results[0], r));
    }
}
